using System.Data;
using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Common;
using SalonBooking.Application.Features.Billing;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Billing;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;
using SalonBooking.Infrastructure.Scheduling;
using SalonBooking.Infrastructure.Security;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Infrastructure.Services;

public sealed class BillingService : IBillingService
{
    private readonly SalonBookingDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IDocumentNumberService _numbers;

    public BillingService(
        SalonBookingDbContext context,
        ICurrentUserService currentUser,
        IDocumentNumberService numbers)
    {
        _context = context;
        _currentUser = currentUser;
        _numbers = numbers;
    }

    public async Task<PagedResult<RetailProductResponse>> ListProductsAsync(
        RetailProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var page = Page(query.Page);
        var pageSize = PageSize(query.PageSize);
        var products = _context.RetailProducts.AsNoTracking()
            .Where(item => item.TenantId == tenantId);
        if (_currentUser.BranchId is > 0)
        {
            var branchId = _currentUser.BranchId.Value;
            products = products.Where(item => item.BranchId == branchId);
        }

        if (!query.IncludeInactive)
        {
            products = products.Where(item => item.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            products = products.Where(item =>
                item.ProductCode.Contains(search) ||
                item.ProductName.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }

        var total = await products.CountAsync(cancellationToken);
        var items = await products
            .OrderBy(item => item.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<RetailProductResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalRecords = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            Items = items.Select(MapProduct).ToList()
        };
    }

    public async Task<RetailProductResponse> CreateProductAsync(
        SaveRetailProductRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsActive)
        {
            throw new BillingException("Create the product as active, then deactivate it.");
        }

        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        var product = new RetailProduct
        {
            TenantId = tenantId,
            BranchId = branchId,
            ProductCode = RequiredCode(request.ProductCode),
            ProductName = RequiredName(request.ProductName),
            Description = Clean(request.Description),
            SellingPrice = NonNegativeMoney(request.SellingPrice, "Selling price")
        };
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        if (await CodeTakenAsync(tenantId, branchId, product.ProductCode, null, cancellationToken))
        {
            throw new BillingException("That product code is already used in this branch.");
        }

        _context.RetailProducts.Add(product);
        await _context.SaveChangesAsync(cancellationToken);
        return MapProduct(product);
    }

    public async Task<RetailProductResponse> UpdateProductAsync(
        long retailProductId,
        SaveRetailProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await RequireProductAsync(retailProductId, cancellationToken);
        var code = RequiredCode(request.ProductCode);
        if (await CodeTakenAsync(product.TenantId, product.BranchId, code, product.RetailProductId, cancellationToken))
        {
            throw new BillingException("That product code is already used in this branch.");
        }

        product.ProductCode = code;
        product.ProductName = RequiredName(request.ProductName);
        product.Description = Clean(request.Description);
        product.SellingPrice = NonNegativeMoney(request.SellingPrice, "Selling price");
        product.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);
        return MapProduct(product);
    }

    public async Task<TaxSettingResponse> GetTaxAsync(long branchId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var resolved = _currentUser.ResolveBranchId(branchId);
        await EnsureBranchAsync(tenantId, resolved, cancellationToken);
        var setting = await _context.TaxSettings.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.BranchId == resolved, cancellationToken);
        return MapTax(resolved, setting);
    }

    public async Task<TaxSettingResponse> SaveTaxAsync(
        SaveTaxSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        var rate = Scaled(request.RatePercent, "Tax rate");
        if (rate < 0 || rate > 100)
        {
            throw new BillingException("The tax rate must be between 0 and 100.");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? "Tax" : request.Name.Trim();
        if (name.Length > 80)
        {
            throw new BillingException("The tax name is too long.");
        }

        var setting = await _context.TaxSettings
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.BranchId == branchId, cancellationToken);
        if (setting == null)
        {
            setting = new TaxSetting
            {
                TenantId = tenantId,
                BranchId = branchId,
                Name = name,
                RatePercent = rate
            };
            _context.TaxSettings.Add(setting);
        }
        else
        {
            setting.Name = name;
            setting.RatePercent = rate;
            setting.IsActive = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return MapTax(branchId, setting);
    }

    public async Task<PagedResult<BillResponse>> ListBillsAsync(
        BillQuery query,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var page = Page(query.Page);
        var pageSize = PageSize(query.PageSize);
        var bills = _context.Bills.AsNoTracking().Where(item => item.TenantId == tenantId);
        if (_currentUser.BranchId is > 0)
        {
            var branchId = _currentUser.BranchId.Value;
            bills = bills.Where(item => item.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = ParseBillStatus(query.Status);
            bills = bills.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.PaymentStatus))
        {
            var paymentStatus = ParsePaymentStatus(query.PaymentStatus);
            bills = bills.Where(item => item.PaymentStatus == paymentStatus);
        }

        if (query.CustomerId is > 0)
        {
            bills = bills.Where(item => item.CustomerId == query.CustomerId);
        }

        if (query.From != null)
        {
            var from = query.From.Value;
            bills = bills.Where(item => item.BillDate >= from);
        }

        if (query.To != null)
        {
            var to = query.To.Value.Date.AddDays(1);
            bills = bills.Where(item => item.BillDate < to);
        }

        var total = await bills.CountAsync(cancellationToken);
        var pageItems = await bills
            .OrderByDescending(item => item.BillDate)
            .ThenByDescending(item => item.BillId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var ids = pageItems.Select(item => item.BillId).ToArray();
        var lines = await _context.BillLines.AsNoTracking()
            .Where(item => ids.Contains(item.BillId))
            .ToListAsync(cancellationToken);
        var payments = await _context.BillPayments.AsNoTracking()
            .Where(item => ids.Contains(item.BillId))
            .ToListAsync(cancellationToken);
        var customerIds = pageItems.Select(item => item.CustomerId).Distinct().ToArray();
        var names = await CustomerNamesAsync(customerIds, cancellationToken);
        return new PagedResult<BillResponse>
        {
            Page = page,
            PageSize = pageSize,
            TotalRecords = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            Items = pageItems.Select(item => MapBill(
                item,
                lines.Where(line => line.BillId == item.BillId),
                payments.Where(payment => payment.BillId == item.BillId),
                names.GetValueOrDefault(item.CustomerId))).ToList()
        };
    }

    public Task<BillResponse> GetBillAsync(long billId, CancellationToken cancellationToken = default) =>
        MapLoadedAsync(billId, cancellationToken);

    public async Task<IReadOnlyList<EligibleServiceLineResponse>> ListEligibleServicesAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        if (appointment.Status != AppointmentStatus.Completed)
        {
            return [];
        }

        var lines = await OpenCompletedLinesAsync(appointment, cancellationToken);
        var claimed = await ClaimedIdsAsync(lines.Select(line => line.AppointmentServiceId).ToArray(), cancellationToken);
        return lines
            .Where(line => !claimed.Contains(line.AppointmentServiceId))
            .OrderBy(line => line.Sequence)
            .Select(line => new EligibleServiceLineResponse
            {
                AppointmentServiceId = line.AppointmentServiceId,
                AppointmentId = line.AppointmentId,
                ServiceId = line.ServiceId,
                ServiceName = line.Service?.ServiceName ?? "Service",
                UnitPrice = line.UnitPrice,
                EmployeeId = line.EmployeeId
            })
            .ToList();
    }

    public async Task<BillingCoverageResponse> GetCoverageAsync(
        long appointmentId,
        CancellationToken cancellationToken = default)
    {
        var appointment = await RequireAppointmentAsync(appointmentId, cancellationToken);
        var response = new BillingCoverageResponse
        {
            AppointmentId = appointment.AppointmentId,
            Coverage = nameof(BillingCoverage.Unbilled)
        };
        if (appointment.Status != AppointmentStatus.Completed)
        {
            return response;
        }

        var lines = await OpenCompletedLinesAsync(appointment, cancellationToken);
        response.BillableLineCount = lines.Count;
        if (lines.Count == 0)
        {
            return response;
        }

        var ids = lines.Select(line => line.AppointmentServiceId).ToArray();
        var claims = await _context.ActiveServiceCharges.IgnoreQueryFilters().AsNoTracking()
            .Where(item => ids.Contains(item.AppointmentServiceId) && item.TenantId == appointment.TenantId)
            .Join(
                _context.Bills.IgnoreQueryFilters().AsNoTracking().Where(bill => !bill.IsDeleted),
                claim => claim.BillId,
                bill => bill.BillId,
                (claim, bill) => bill.Status)
            .ToListAsync(cancellationToken);
        response.DraftLineCount = claims.Count(status => status == BillStatus.Draft);
        response.FinalizedLineCount = claims.Count(status => status == BillStatus.Finalized);
        response.UnbilledLineCount = lines.Count - claims.Count;
        response.Coverage = response.FinalizedLineCount == lines.Count
            ? nameof(BillingCoverage.FullyBilled)
            : response.FinalizedLineCount > 0
                ? nameof(BillingCoverage.PartiallyBilled)
                : response.DraftLineCount > 0
                    ? nameof(BillingCoverage.DraftInProgress)
                    : nameof(BillingCoverage.Unbilled);
        return response;
    }

    public async Task<BillResponse> CreateBillAsync(
        CreateBillRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.RequireTenantId();
        var branchId = _currentUser.ResolveBranchId(request.BranchId);
        await EnsureBranchAsync(tenantId, branchId, cancellationToken);
        var customer = await RequireCustomerAsync(request.CustomerId, branchId, cancellationToken);
        var serviceLines = await TakeServicesAsync(
            tenantId,
            branchId,
            customer.CustomerId,
            request.AppointmentId,
            request.AppointmentServiceIds,
            cancellationToken);
        var products = await TakeProductsAsync(tenantId, branchId, request.Products, cancellationToken);
        var appointmentId = serviceLines.Count == 0
            ? (long?)null
            : serviceLines[0].AppointmentId;
        if (request.AppointmentId is > 0 && serviceLines.Count > 0 && request.AppointmentId != appointmentId)
        {
            throw new BillingException("A bill cannot combine more than one appointment.");
        }

        var billId = await InTransactionAsync(async () =>
        {
            var rate = await RateAsync(tenantId, branchId, cancellationToken);
            var bill = new Bill
            {
                TenantId = tenantId,
                BranchId = branchId,
                CustomerId = customer.CustomerId,
                AppointmentId = appointmentId,
                Status = BillStatus.Draft,
                PaymentStatus = PaymentStatus.Unpaid,
                BillDate = DateTime.UtcNow,
                Notes = Clean(request.Notes)
            };
            PrepareVersion(bill);
            var lines = new List<BillLine>();
            foreach (var service in serviceLines)
            {
                var line = ServiceLine(bill, service, rate);
                lines.Add(line);
                _context.ActiveServiceCharges.Add(new ActiveServiceCharge
                {
                    AppointmentServiceId = service.AppointmentServiceId,
                    TenantId = tenantId,
                    BranchId = branchId,
                    Bill = bill,
                    BillLine = line
                });
            }

            foreach (var (product, quantity, price) in products)
            {
                lines.Add(ProductLine(bill, product, quantity, price, rate));
            }

            ApplyTotals(bill, lines, rate);
            _context.Bills.Add(bill);
            _context.BillLines.AddRange(lines);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public async Task<BillResponse> AddServiceLinesAsync(
        long billId,
        AddServiceLinesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.AppointmentServiceIds.Count == 0)
        {
            throw new BillingException("Choose a service line.");
        }

        await InTransactionAsync(async () =>
        {
            var bill = await RequireDraftAsync(billId, request.RowVersion, cancellationToken);
            var services = await TakeServicesAsync(
                bill.TenantId,
                bill.BranchId,
                bill.CustomerId,
                bill.AppointmentId,
                request.AppointmentServiceIds,
                cancellationToken);
            if (bill.AppointmentId is > 0 && services.Any(line => line.AppointmentId != bill.AppointmentId))
            {
                throw new BillingException("A bill cannot combine more than one appointment.");
            }

            bill.AppointmentId ??= services[0].AppointmentId;
            var rate = await RateAsync(bill.TenantId, bill.BranchId, cancellationToken);
            foreach (var service in services)
            {
                var line = ServiceLine(bill, service, rate);
                _context.BillLines.Add(line);
                _context.ActiveServiceCharges.Add(new ActiveServiceCharge
                {
                    AppointmentServiceId = service.AppointmentServiceId,
                    TenantId = bill.TenantId,
                    BranchId = bill.BranchId,
                    Bill = bill,
                    BillLine = line
                });
            }

            await RecalculateAsync(bill, cancellationToken);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public async Task<BillResponse> AddProductLineAsync(
        long billId,
        AddProductLineRequest request,
        CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async () =>
        {
            var bill = await RequireDraftAsync(billId, request.RowVersion, cancellationToken);
            var taken = await TakeProductsAsync(
                bill.TenantId,
                bill.BranchId,
                [new ProductLineInput
                {
                    RetailProductId = request.RetailProductId,
                    Quantity = request.Quantity,
                    UnitPrice = request.UnitPrice
                }],
                cancellationToken);
            var (product, quantity, price) = taken[0];
            var rate = await RateAsync(bill.TenantId, bill.BranchId, cancellationToken);
            _context.BillLines.Add(ProductLine(bill, product, quantity, price, rate));
            await RecalculateAsync(bill, cancellationToken);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public async Task<BillResponse> AddRentalLineAsync(
        long billId,
        AddRentalLineRequest request,
        CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async () =>
        {
            var bill = await RequireDraftAsync(billId, request.RowVersion, cancellationToken);
            var reservation = await _context.RentalReservations.Include(item => item.Asset)
                .FirstOrDefaultAsync(item => item.RentalReservationId == request.RentalReservationId, cancellationToken)
                ?? throw new KeyNotFoundException();
            if (reservation.BranchId != bill.BranchId || reservation.CustomerId != bill.CustomerId)
            {
                throw new BillingException("The rental belongs to a different customer or branch.");
            }

            if (reservation.Status != RentalReservationStatus.Returned)
            {
                throw new BillingException("A rental is billed once, after it is returned.");
            }

            var claimed = await _context.ActiveRentalCharges.AnyAsync(
                item => item.RentalReservationId == reservation.RentalReservationId, cancellationToken);
            if (claimed)
            {
                throw new BillingConflictException("That rental is already on a bill.");
            }

            var rate = await RateAsync(bill.TenantId, bill.BranchId, cancellationToken);
            var line = new BillLine
            {
                TenantId = bill.TenantId,
                BranchId = bill.BranchId,
                Bill = bill,
                LineType = BillLineType.Rental,
                Description = reservation.Asset?.AssetName ?? "Rental",
                Quantity = 1,
                UnitPrice = reservation.ChargePerUse,
                RentalReservationId = reservation.RentalReservationId
            };
            ApplyTotals(bill, [line], rate);
            _context.BillLines.Add(line);
            await _context.SaveChangesAsync(cancellationToken);
            _context.ActiveRentalCharges.Add(new ActiveRentalCharge
            {
                RentalReservationId = reservation.RentalReservationId,
                BillId = bill.BillId,
                BillLineId = line.BillLineId,
                TenantId = bill.TenantId,
                BranchId = bill.BranchId
            });
            await RecalculateAsync(bill, cancellationToken);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public async Task<BillResponse> UpdateProductLineAsync(
        long billId,
        long billLineId,
        UpdateProductLineRequest request,
        CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async () =>
        {
            var bill = await RequireDraftAsync(billId, request.RowVersion, cancellationToken);
            var line = await RequireLineAsync(bill, billLineId, cancellationToken);
            if (line.LineType != BillLineType.Product)
            {
                throw new BillingException("Only a retail product line can change quantity or price.");
            }

            line.Quantity = Quantity(request.Quantity);
            line.UnitPrice = NonNegativeMoney(request.UnitPrice, "Unit price");
            await RecalculateAsync(bill, cancellationToken);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public async Task<BillResponse> RemoveLineAsync(
        long billId,
        long billLineId,
        string rowVersion,
        CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async () =>
        {
            var bill = await RequireDraftAsync(billId, rowVersion, cancellationToken);
            var line = await RequireLineAsync(bill, billLineId, cancellationToken);
            if (line.LineType == BillLineType.Service)
            {
                var claim = await _context.ActiveServiceCharges
                    .FirstOrDefaultAsync(item => item.BillLineId == line.BillLineId, cancellationToken);
                if (claim != null)
                {
                    _context.ActiveServiceCharges.Remove(claim);
                }
            }

            if (line.LineType == BillLineType.Rental)
            {
                var rentalClaim = await _context.ActiveRentalCharges
                    .FirstOrDefaultAsync(item => item.BillLineId == line.BillLineId, cancellationToken);
                if (rentalClaim != null)
                {
                    _context.ActiveRentalCharges.Remove(rentalClaim);
                }
            }

            _context.BillLines.Remove(line);
            await _context.SaveChangesAsync(cancellationToken);
            var remainingServices = await _context.BillLines.AnyAsync(
                item => item.BillId == bill.BillId && item.LineType == BillLineType.Service,
                cancellationToken);
            if (!remainingServices)
            {
                bill.AppointmentId = null;
            }

            await RecalculateAsync(bill, cancellationToken);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public async Task<BillResponse> FinalizeAsync(
        long billId,
        string? rowVersion,
        CancellationToken cancellationToken = default)
    {
        await InTransactionAsync(async () =>
        {
            var bill = await RequireDraftAsync(billId, rowVersion, cancellationToken);
            var lines = await _context.BillLines.Where(item => item.BillId == bill.BillId).ToListAsync(cancellationToken);
            if (lines.Count == 0)
            {
                throw new BillingException("Add a line before finalizing the bill.");
            }

            await RevalidateServicesAsync(bill, lines, cancellationToken);
            await RecalculateAsync(bill, cancellationToken);
            bill.BillNumber = await _numbers.NextCodeAsync(bill.TenantId, Application.Documents.DocSeriesKind.Bill, cancellationToken);
            bill.Status = BillStatus.Finalized;
            bill.FinalizedAt = DateTime.UtcNow;
            var net = await NetCollectedAsync(bill.BillId, cancellationToken);
            bill.PaymentStatus = Money.PaymentState(bill.Total, net, true);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    public Task<BillResponse> RecordPaymentAsync(
        long billId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default) =>
        RecordMovementAsync(billId, request, PaymentKind.Payment, cancellationToken);

    public Task<BillResponse> RecordRefundAsync(
        long billId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default) =>
        RecordMovementAsync(billId, request, PaymentKind.Refund, cancellationToken);

    public async Task<BillResponse> VoidAsync(
        long billId,
        VoidBillRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
        {
            throw new BillingException("A void reason is required.");
        }

        if (reason.Length > 500)
        {
            throw new BillingException("The void reason is too long.");
        }

        await InTransactionAsync(async () =>
        {
            var bill = await RequireBillAsync(billId, cancellationToken);
            ApplyOptionalVersion(bill, request.RowVersion);
            if (bill.Status == BillStatus.Void)
            {
                throw new BillingException("The bill is already void.");
            }

            var net = await NetCollectedAsync(bill.BillId, cancellationToken);
            if (net != 0)
            {
                throw new BillingException("Refund the amount collected before voiding the bill.");
            }

            var claims = await _context.ActiveServiceCharges
                .Where(item => item.BillId == bill.BillId)
                .ToListAsync(cancellationToken);
            _context.ActiveServiceCharges.RemoveRange(claims);
            var rentalClaims = await _context.ActiveRentalCharges
                .Where(item => item.BillId == bill.BillId)
                .ToListAsync(cancellationToken);
            _context.ActiveRentalCharges.RemoveRange(rentalClaims);
            bill.Status = BillStatus.Void;
            bill.VoidedAt = DateTime.UtcNow;
            bill.VoidedByUserId = _currentUser.UserId;
            bill.VoidReason = reason;
            bill.PaymentStatus = PaymentStatus.Unpaid;
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    private async Task<BillResponse> RecordMovementAsync(
        long billId,
        RecordPaymentRequest request,
        PaymentKind kind,
        CancellationToken cancellationToken)
    {
        var amount = PositiveMoney(request.Amount, kind == PaymentKind.Payment ? "Payment" : "Refund");
        var method = ParseMethod(request.Method);
        await InTransactionAsync(async () =>
        {
            var bill = await RequireBillAsync(billId, cancellationToken);
            ApplyOptionalVersion(bill, request.RowVersion);
            if (bill.Status != BillStatus.Finalized)
            {
                throw new BillingException("Record payments only on a finalized bill.");
            }

            var net = await NetCollectedAsync(bill.BillId, cancellationToken);
            if (kind == PaymentKind.Payment)
            {
                var outstanding = Money.Outstanding(bill.Total, net);
                if (amount > outstanding)
                {
                    throw new BillingException("The payment is more than the outstanding balance.");
                }
            }
            else if (amount > net)
            {
                throw new BillingException("The refund is more than the amount collected.");
            }

            _context.BillPayments.Add(new BillPayment
            {
                TenantId = bill.TenantId,
                BranchId = bill.BranchId,
                BillId = bill.BillId,
                Kind = kind,
                Method = method,
                Amount = kind == PaymentKind.Payment ? amount : -amount,
                RecordedAt = DateTime.UtcNow,
                RecordedByUserId = _currentUser.UserId,
                Notes = Clean(request.Notes)
            });
            var updated = kind == PaymentKind.Payment ? net + amount : net - amount;
            bill.PaymentStatus = Money.PaymentState(bill.Total, updated, true);
            Touch(bill);
            await SaveBillAsync(cancellationToken);
            return bill.BillId;
        }, cancellationToken);
        return await MapLoadedAsync(billId, cancellationToken);
    }

    private async Task RevalidateServicesAsync(
        Bill bill,
        IReadOnlyList<BillLine> lines,
        CancellationToken cancellationToken)
    {
        var serviceLines = lines.Where(line => line.LineType == BillLineType.Service).ToList();
        if (serviceLines.Count == 0)
        {
            return;
        }

        var ids = serviceLines.Select(line => line.AppointmentServiceId!.Value).ToArray();
        var services = await _context.AppointmentServices.IgnoreQueryFilters()
            .Include(line => line.Appointment)
            .Where(line => ids.Contains(line.AppointmentServiceId) && line.TenantId == bill.TenantId && !line.IsDeleted)
            .ToListAsync(cancellationToken);
        if (services.Count != ids.Length)
        {
            throw new BillingException("A service on this bill is no longer available.");
        }

        var claims = await _context.ActiveServiceCharges
            .Where(item => item.BillId == bill.BillId)
            .ToListAsync(cancellationToken);
        foreach (var line in serviceLines)
        {
            var service = services.Single(item => item.AppointmentServiceId == line.AppointmentServiceId);
            if (service.Appointment.Status != AppointmentStatus.Completed ||
                service.LineStatus != ServiceLineStatus.Completed ||
                service.IsCancelled ||
                service.Appointment.CustomerId != bill.CustomerId ||
                service.BranchId != bill.BranchId)
            {
                throw new BillingException("A service line is no longer eligible to bill.");
            }

            if (!claims.Any(item =>
                    item.AppointmentServiceId == service.AppointmentServiceId &&
                    item.BillLineId == line.BillLineId))
            {
                throw new BillingException("A service claim is missing. Reload the bill.");
            }
        }
    }

    private async Task<List<AppointmentService>> TakeServicesAsync(
        long tenantId,
        long branchId,
        long customerId,
        long? expectedAppointmentId,
        IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var distinct = ids.Distinct().ToArray();
        if (distinct.Length != ids.Count)
        {
            throw new BillingException("The same service line was selected twice.");
        }

        var lines = await _context.AppointmentServices.IgnoreQueryFilters()
            .Include(line => line.Service)
            .Include(line => line.Appointment)
            .Where(line =>
                distinct.Contains(line.AppointmentServiceId) &&
                line.TenantId == tenantId &&
                !line.IsDeleted)
            .ToListAsync(cancellationToken);
        if (lines.Count != distinct.Length)
        {
            throw new KeyNotFoundException();
        }

        foreach (var line in lines)
        {
            _currentUser.EnsureCanAccessBranch(line.BranchId);
        }

        if (lines.Any(line => line.BranchId != branchId))
        {
            throw new BillingException("The service is not in this branch.");
        }

        if (lines.Select(line => line.AppointmentId).Distinct().Count() != 1)
        {
            throw new BillingException("A bill cannot combine more than one appointment.");
        }

        if (expectedAppointmentId is > 0 && lines[0].AppointmentId != expectedAppointmentId)
        {
            throw new BillingException("A bill cannot combine more than one appointment.");
        }

        if (lines.Any(line =>
                line.Appointment.CustomerId != customerId ||
                line.Appointment.Status != AppointmentStatus.Completed ||
                line.LineStatus != ServiceLineStatus.Completed ||
                line.IsCancelled))
        {
            throw new BillingException("Only completed services on a completed appointment can be billed.");
        }

        var claimed = await ClaimedIdsAsync(distinct, cancellationToken);
        if (claimed.Count > 0)
        {
            throw new BillingConflictException("That service is already on a bill.");
        }

        return lines;
    }

    private async Task<List<(RetailProduct Product, int Quantity, decimal Price)>> TakeProductsAsync(
        long tenantId,
        long branchId,
        IReadOnlyList<ProductLineInput> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var ids = inputs.Select(item => item.RetailProductId).Distinct().ToArray();
        var products = await _context.RetailProducts
            .Where(item => ids.Contains(item.RetailProductId) && item.TenantId == tenantId && item.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var result = new List<(RetailProduct, int, decimal)>();
        foreach (var input in inputs)
        {
            var product = products.SingleOrDefault(item => item.RetailProductId == input.RetailProductId);
            if (product == null || !product.IsActive)
            {
                throw new BillingException("Choose an active retail product from this branch.");
            }

            var price = input.UnitPrice == null
                ? product.SellingPrice
                : NonNegativeMoney(input.UnitPrice.Value, "Unit price");
            result.Add((product, Quantity(input.Quantity), price));
        }

        return result;
    }

    private async Task RecalculateAsync(Bill bill, CancellationToken cancellationToken)
    {
        var persisted = await _context.BillLines.Where(item => item.BillId == bill.BillId).ToListAsync(cancellationToken);
        var added = _context.BillLines.Local
            .Where(item => item.Bill == bill && _context.Entry(item).State == EntityState.Added)
            .ToList();
        var rate = await RateAsync(bill.TenantId, bill.BranchId, cancellationToken);
        ApplyTotals(bill, persisted.Concat(added).Distinct(), rate);
    }

    private static void ApplyTotals(Bill bill, IEnumerable<BillLine> lines, decimal rate)
    {
        decimal subtotal = 0;
        decimal tax = 0;
        foreach (var line in lines)
        {
            line.DiscountAmount = 0;
            line.TaxRatePercent = rate;
            var net = Money.LineNet(line.Quantity, line.UnitPrice, 0);
            var taxAmount = Money.TaxAmount(net, rate);
            line.TaxAmount = taxAmount;
            line.LineTotal = net + taxAmount;
            subtotal += net;
            tax += taxAmount;
        }

        bill.Subtotal = subtotal;
        bill.TaxAmount = tax;
        bill.Total = subtotal + tax;
        if (bill.Status != BillStatus.Finalized)
        {
            bill.PaymentStatus = PaymentStatus.Unpaid;
        }
    }

    private static BillLine ServiceLine(Bill bill, AppointmentService service, decimal rate)
    {
        var line = new BillLine
        {
            TenantId = bill.TenantId,
            BranchId = bill.BranchId,
            Bill = bill,
            LineType = BillLineType.Service,
            Description = service.Service?.ServiceName ?? "Service",
            Quantity = 1,
            UnitPrice = service.UnitPrice,
            AppointmentServiceId = service.AppointmentServiceId
        };
        ApplyTotals(bill, [line], rate);
        return line;
    }

    private static BillLine ProductLine(
        Bill bill,
        RetailProduct product,
        int quantity,
        decimal price,
        decimal rate)
    {
        var line = new BillLine
        {
            TenantId = bill.TenantId,
            BranchId = bill.BranchId,
            Bill = bill,
            LineType = BillLineType.Product,
            Description = product.ProductName,
            Quantity = quantity,
            UnitPrice = price,
            RetailProductId = product.RetailProductId
        };
        ApplyTotals(bill, [line], rate);
        return line;
    }

    private async Task<decimal> RateAsync(long tenantId, long branchId, CancellationToken cancellationToken)
    {
        var rate = await _context.TaxSettings.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.BranchId == branchId && item.IsActive)
            .Select(item => (decimal?)item.RatePercent)
            .FirstOrDefaultAsync(cancellationToken);
        return rate ?? 0;
    }

    private async Task<HashSet<long>> ClaimedIdsAsync(IReadOnlyCollection<long> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var claimed = await _context.ActiveServiceCharges.IgnoreQueryFilters()
            .Where(item => ids.Contains(item.AppointmentServiceId))
            .Select(item => item.AppointmentServiceId)
            .ToListAsync(cancellationToken);
        return claimed.ToHashSet();
    }

    private async Task<List<AppointmentService>> OpenCompletedLinesAsync(
        Appointment appointment,
        CancellationToken cancellationToken)
    {
        return await _context.AppointmentServices.IgnoreQueryFilters()
            .Include(line => line.Service)
            .Where(line =>
                line.AppointmentId == appointment.AppointmentId &&
                line.TenantId == appointment.TenantId &&
                !line.IsDeleted &&
                !line.IsCancelled &&
                line.LineStatus == ServiceLineStatus.Completed)
            .ToListAsync(cancellationToken);
    }

    private async Task<decimal> NetCollectedAsync(long billId, CancellationToken cancellationToken)
    {
        var amounts = await _context.BillPayments.AsNoTracking()
            .Where(item => item.BillId == billId)
            .Select(item => item.Amount)
            .ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await action();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private async Task SaveBillAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BillingConflictException("The bill was changed by someone else. Reload it and try again.");
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new BillingConflictException("That service is already on a bill.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("2601", StringComparison.Ordinal) ||
            message.Contains("2627", StringComparison.Ordinal);
    }

    private async Task<BillResponse> MapLoadedAsync(long billId, CancellationToken cancellationToken)
    {
        var bill = await RequireBillAsync(billId, cancellationToken);
        var lines = await _context.BillLines.AsNoTracking()
            .Where(item => item.BillId == bill.BillId)
            .OrderBy(item => item.BillLineId)
            .ToListAsync(cancellationToken);
        var payments = await _context.BillPayments.AsNoTracking()
            .Where(item => item.BillId == bill.BillId)
            .OrderBy(item => item.RecordedAt)
            .ThenBy(item => item.BillPaymentId)
            .ToListAsync(cancellationToken);
        var names = await CustomerNamesAsync([bill.CustomerId], cancellationToken);
        return MapBill(bill, lines, payments, names.GetValueOrDefault(bill.CustomerId));
    }

    private async Task<Dictionary<long, string>> CustomerNamesAsync(
        IReadOnlyCollection<long> customerIds,
        CancellationToken cancellationToken)
    {
        if (customerIds.Count == 0)
        {
            return [];
        }

        var rows = await _context.Customers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => customerIds.Contains(item.CustomerId))
            .Select(item => new { item.CustomerId, item.FirstName, item.LastName })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(
            item => item.CustomerId,
            item => $"{item.FirstName} {item.LastName}".Trim());
    }

    private async Task<Bill> RequireBillAsync(long billId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var bill = await _context.Bills.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.BillId == billId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (bill == null)
        {
            throw new KeyNotFoundException();
        }

        _currentUser.EnsureCanAccessBranch(bill.BranchId);
        return bill;
    }

    private async Task<Bill> RequireDraftAsync(long billId, string? rowVersion, CancellationToken cancellationToken)
    {
        var bill = await RequireBillAsync(billId, cancellationToken);
        if (bill.Status != BillStatus.Draft)
        {
            throw new BillingException("A finalized bill cannot be edited.");
        }

        if (string.IsNullOrWhiteSpace(rowVersion))
        {
            throw new BillingException("Reload the bill before changing it.");
        }

        _context.Entry(bill).Property(item => item.RowVersion).OriginalValue = SchedulingGuard.ReadRowVersion(rowVersion);
        return bill;
    }

    private void ApplyOptionalVersion(Bill bill, string? rowVersion)
    {
        if (!string.IsNullOrWhiteSpace(rowVersion))
        {
            _context.Entry(bill).Property(item => item.RowVersion).OriginalValue =
                SchedulingGuard.ReadRowVersion(rowVersion);
        }
    }

    private async Task<BillLine> RequireLineAsync(Bill bill, long billLineId, CancellationToken cancellationToken)
    {
        var line = await _context.BillLines
            .FirstOrDefaultAsync(item => item.BillLineId == billLineId && item.BillId == bill.BillId, cancellationToken);
        if (line == null)
        {
            throw new KeyNotFoundException();
        }

        return line;
    }

    private async Task<Appointment> RequireAppointmentAsync(long appointmentId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var appointment = await _context.Appointments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.AppointmentId == appointmentId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (appointment == null)
        {
            throw new KeyNotFoundException();
        }

        _currentUser.EnsureCanAccessBranch(appointment.BranchId);
        return appointment;
    }

    private async Task<Customer> RequireCustomerAsync(long customerId, long branchId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var customer = await _context.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.CustomerId == customerId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (customer == null)
        {
            throw new KeyNotFoundException();
        }

        _currentUser.EnsureCanAccessBranch(customer.BranchId);
        if (customer.BranchId != branchId || !customer.IsActive)
        {
            throw new BillingException("Choose an active customer in this branch.");
        }

        return customer;
    }

    private async Task<RetailProduct> RequireProductAsync(long retailProductId, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.RequireTenantId();
        var product = await _context.RetailProducts.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.RetailProductId == retailProductId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (product == null)
        {
            throw new KeyNotFoundException();
        }

        _currentUser.EnsureCanAccessBranch(product.BranchId);
        return product;
    }

    private async Task EnsureBranchAsync(long tenantId, long branchId, CancellationToken cancellationToken)
    {
        var exists = await _context.Branches.IgnoreQueryFilters().AnyAsync(
            item => item.BranchId == branchId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted,
            cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException();
        }
    }

    private async Task<bool> CodeTakenAsync(
        long tenantId,
        long branchId,
        string code,
        long? exceptId,
        CancellationToken cancellationToken)
    {
        return await _context.RetailProducts.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.BranchId == branchId &&
                    item.ProductCode == code &&
                    item.RetailProductId != exceptId,
            cancellationToken);
    }

    private void PrepareVersion(Bill bill)
    {
        if (!_context.Database.IsSqlServer())
        {
            bill.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }

    private void Touch(Bill bill)
    {
        if (!_context.Database.IsSqlServer())
        {
            bill.RowVersion = Guid.NewGuid().ToByteArray();
        }
    }

    private static BillResponse MapBill(
        Bill bill,
        IEnumerable<BillLine> lines,
        IEnumerable<BillPayment> payments,
        string? customerName)
    {
        var paymentList = payments.OrderBy(item => item.RecordedAt).ThenBy(item => item.BillPaymentId).ToList();
        var net = paymentList.Sum(item => item.Amount);
        return new BillResponse
        {
            BillId = bill.BillId,
            BranchId = bill.BranchId,
            CustomerId = bill.CustomerId,
            CustomerName = customerName,
            AppointmentId = bill.AppointmentId,
            BillNumber = bill.BillNumber,
            Status = bill.Status.ToString(),
            PaymentStatus = bill.PaymentStatus.ToString(),
            BillDate = bill.BillDate,
            Subtotal = bill.Subtotal,
            TaxAmount = bill.TaxAmount,
            Total = bill.Total,
            NetCollected = net,
            Outstanding = bill.Status == BillStatus.Finalized ? Money.Outstanding(bill.Total, net) : bill.Total,
            Notes = bill.Notes,
            FinalizedAt = bill.FinalizedAt,
            VoidedAt = bill.VoidedAt,
            VoidReason = bill.VoidReason,
            RowVersion = bill.RowVersion.Length == 0 ? string.Empty : SchedulingGuard.WriteRowVersion(bill.RowVersion),
            Lines = lines.OrderBy(item => item.BillLineId).Select(line => new BillLineResponse
            {
                BillLineId = line.BillLineId,
                LineType = line.LineType.ToString(),
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.DiscountAmount,
                TaxRatePercent = line.TaxRatePercent,
                TaxAmount = line.TaxAmount,
                LineTotal = line.LineTotal,
                AppointmentServiceId = line.AppointmentServiceId,
                RetailProductId = line.RetailProductId,
                RentalReservationId = line.RentalReservationId
            }).ToList(),
            Payments = paymentList.Select(item => new BillPaymentResponse
            {
                BillPaymentId = item.BillPaymentId,
                Kind = item.Kind.ToString(),
                Method = item.Method.ToString(),
                Amount = item.Amount,
                RecordedAt = item.RecordedAt,
                RecordedByUserId = item.RecordedByUserId,
                Notes = item.Notes
            }).ToList()
        };
    }

    private static RetailProductResponse MapProduct(RetailProduct product) => new()
    {
        RetailProductId = product.RetailProductId,
        BranchId = product.BranchId,
        ProductCode = product.ProductCode,
        ProductName = product.ProductName,
        Description = product.Description,
        SellingPrice = product.SellingPrice,
        IsActive = product.IsActive
    };

    private static TaxSettingResponse MapTax(long branchId, TaxSetting? setting) => new()
    {
        BranchId = branchId,
        Configured = setting != null,
        Name = setting?.Name ?? "Tax",
        RatePercent = setting?.RatePercent ?? 0
    };

    private static string RequiredCode(string? value)
    {
        var code = value?.Trim() ?? string.Empty;
        if (code.Length == 0 || code.Length > 40)
        {
            throw new BillingException("Enter a product code of up to 40 characters.");
        }

        return code;
    }

    private static string RequiredName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 100)
        {
            throw new BillingException("Enter a product name of up to 100 characters.");
        }

        return name;
    }

    private static string? Clean(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int Quantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BillingException("Quantity must be at least 1.");
        }

        return quantity;
    }

    private static decimal NonNegativeMoney(decimal value, string name)
    {
        var amount = Scaled(value, name);
        if (amount < 0)
        {
            throw new BillingException($"{name} cannot be negative.");
        }

        return amount;
    }

    private static decimal Scaled(decimal value, string name)
    {
        try
        {
            return Money.RequireScale(value, name);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new BillingException($"{name} must use at most two decimal places.");
        }
    }

    private static decimal PositiveMoney(decimal value, string name)
    {
        var amount = NonNegativeMoney(value, name);
        if (amount <= 0)
        {
            throw new BillingException($"{name} must be greater than zero.");
        }

        return amount;
    }

    private static PaymentMethod ParseMethod(string? value)
    {
        if (Enum.TryParse<PaymentMethod>(value?.Trim(), true, out var method))
        {
            return method;
        }

        throw new BillingException("Choose cash, card, bank transfer, or other.");
    }

    private static BillStatus ParseBillStatus(string value)
    {
        if (Enum.TryParse<BillStatus>(value.Trim(), true, out var status))
        {
            return status;
        }

        throw new BillingException("That bill status is not recognised.");
    }

    private static PaymentStatus ParsePaymentStatus(string value)
    {
        if (Enum.TryParse<PaymentStatus>(value.Trim(), true, out var status))
        {
            return status;
        }

        throw new BillingException("That payment status is not recognised.");
    }

    private static int Page(int page) => page < 1 ? 1 : page;

    private static int PageSize(int pageSize) => pageSize < 1 ? 50 : Math.Min(pageSize, 100);
}
