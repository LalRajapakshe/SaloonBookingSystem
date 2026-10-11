using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class CustomerPhotoTests
{
    private readonly ApiFactory _factory;

    public CustomerPhotoTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customer_is_created_without_a_photo()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);

        var created = await client.PostAsJsonAsync("/api/Customers", new
        {
            firstName = "No",
            lastName = "Photo",
            mobileNo = "0781000001"
        });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var customer = await created.Content.ReadFromJsonAsync<PhotoCustomer>();
        Assert.NotNull(customer);
        Assert.Null(customer.PhotoPath);

        var before = Directory.GetFiles(_factory.PhotoRoot).Length;
        var photo = await client.GetAsync($"/api/Customers/{customer.CustomerId}/photo");
        Assert.Equal(HttpStatusCode.NotFound, photo.StatusCode);
        Assert.Equal(before, Directory.GetFiles(_factory.PhotoRoot).Length);
    }

    [Fact]
    public async Task Authorized_user_can_upload_replace_and_remove_a_photo()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        var customerId = await CreateCustomerAsync(client, "0781000002");

        var first = await client.PostAsync(
            $"/api/Customers/{customerId}/photo",
            PhotoContent(MinimalJpeg(), "ignored-name.png"));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var saved = await first.Content.ReadFromJsonAsync<PhotoCustomer>();
        Assert.NotNull(saved);
        Assert.EndsWith(".jpg", saved.PhotoPath);
        Assert.DoesNotContain("\\", saved.PhotoPath);
        Assert.DoesNotContain("/", saved.PhotoPath);

        var downloaded = await client.GetAsync($"/api/Customers/{customerId}/photo");
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal("image/jpeg", downloaded.Content.Headers.ContentType?.MediaType);
        Assert.Equal(MinimalJpeg(), await downloaded.Content.ReadAsByteArrayAsync());

        var replaced = await client.PostAsync(
            $"/api/Customers/{customerId}/photo",
            PhotoContent(MinimalPng(), "portrait.jpg"));
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var updated = await replaced.Content.ReadFromJsonAsync<PhotoCustomer>();
        Assert.EndsWith(".png", updated!.PhotoPath);
        Assert.Single(Directory.GetFiles(_factory.PhotoRoot), path => Path.GetFileName(path) == updated.PhotoPath);

        var png = await client.GetAsync($"/api/Customers/{customerId}/photo");
        Assert.Equal("image/png", png.Content.Headers.ContentType?.MediaType);

        var removed = await client.DeleteAsync($"/api/Customers/{customerId}/photo");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        var cleared = await removed.Content.ReadFromJsonAsync<PhotoCustomer>();
        Assert.Null(cleared!.PhotoPath);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/Customers/{customerId}/photo")).StatusCode);
        Assert.False(File.Exists(Path.Combine(_factory.PhotoRoot, updated.PhotoPath!)));
    }

    [Fact]
    public async Task Invalid_and_oversized_photos_are_rejected_without_a_stored_file()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        var customerId = await CreateCustomerAsync(client, "0781000003");
        var before = Directory.GetFiles(_factory.PhotoRoot).Length;

        var invalid = await client.PostAsync(
            $"/api/Customers/{customerId}/photo",
            PhotoContent(Encoding.UTF8.GetBytes("not-an-image"), "photo.jpg"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("JPEG", await invalid.Content.ReadAsStringAsync());

        var oversized = await client.PostAsync(
            $"/api/Customers/{customerId}/photo",
            PhotoContent(new byte[2049], "photo.jpg"));
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        Assert.Contains("larger", await oversized.Content.ReadAsStringAsync());

        var customer = await client.GetFromJsonAsync<PhotoCustomer>($"/api/Customers/{customerId}");
        Assert.Null(customer!.PhotoPath);
        Assert.Equal(before, Directory.GetFiles(_factory.PhotoRoot).Length);
    }

    [Fact]
    public async Task Another_tenant_cannot_read_or_change_a_customer_photo()
    {
        using var owner = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        var customerId = await CreateCustomerAsync(owner, "0781000004");
        var uploaded = await owner.PostAsync(
            $"/api/Customers/{customerId}/photo",
            PhotoContent(MinimalJpeg(), "photo.jpg"));
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);

        using var other = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserBName);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/Customers/{customerId}/photo")).StatusCode);
        var forbiddenUpload = await other.PostAsync(
            $"/api/Customers/{customerId}/photo",
            PhotoContent(MinimalPng(), "photo.png"));
        Assert.Equal(HttpStatusCode.NotFound, forbiddenUpload.StatusCode);

        var stillThere = await owner.GetAsync($"/api/Customers/{customerId}/photo");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
        Assert.Equal("image/jpeg", stillThere.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Failed_database_update_removes_the_new_file_and_keeps_the_old_reference()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(ApiFactory.BranchUserAName);
        var customerId = await CreateCustomerAsync(client, "0781000005");
        var before = Directory.GetFiles(_factory.PhotoRoot).Length;

        try
        {
            await _factory.ReadAsync(db => db.Database.ExecuteSqlRawAsync("PRAGMA query_only = ON;"));
            var response = await client.PostAsync(
                $"/api/Customers/{customerId}/photo",
                PhotoContent(MinimalJpeg(), "photo.jpg"));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        finally
        {
            await _factory.ReadAsync(db => db.Database.ExecuteSqlRawAsync("PRAGMA query_only = OFF;"));
        }

        var customer = await client.GetFromJsonAsync<PhotoCustomer>($"/api/Customers/{customerId}");
        Assert.Null(customer!.PhotoPath);
        Assert.Equal(before, Directory.GetFiles(_factory.PhotoRoot).Length);
    }

    [Fact]
    public async Task Photo_requests_without_a_token_are_rejected()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/Customers/{_factory.CustomerA1Id}/photo");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<long> CreateCustomerAsync(HttpClient client, string mobileNo)
    {
        var created = await client.PostAsJsonAsync("/api/Customers", new
        {
            firstName = "Photo",
            lastName = "Customer",
            mobileNo
        });
        created.EnsureSuccessStatusCode();
        var customer = await created.Content.ReadFromJsonAsync<PhotoCustomer>();
        return customer!.CustomerId;
    }

    private static MultipartFormDataContent PhotoContent(byte[] bytes, string fileName)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", fileName);
        return content;
    }

    private static byte[] MinimalJpeg() =>
    [
        0xFF, 0xD8,
        0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00,
        0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00,
        0x7F, 0xFF, 0x00, 0x11,
        0xFF, 0xD9
    ];

    private static byte[] MinimalPng() =>
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private sealed class PhotoCustomer
    {
        public long CustomerId { get; set; }

        public string? PhotoPath { get; set; }
    }
}
