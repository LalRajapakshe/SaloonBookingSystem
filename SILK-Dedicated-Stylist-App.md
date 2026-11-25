# Dedicated Stylist App — future SILK workstream

This document records the dedicated stylist application as a later SILK workstream. It is not implemented in the current staff or customer apps. The staff app’s My Work screen remains the stylist experience until that workstream is approved.

## Why it is separate

Stylists need a small signed-in view of their own assigned work. The staff application also serves administrators, reception, and supervisors, and it exposes rental, billing, and salon administration to those roles. A dedicated app keeps that surface out of the stylist’s hands without copying a second set of booking rules.

The current staff app already resolves the signed-in stylist on the server. `GET /api/Appointments/mine` uses the employee claim on the token. The client does not send an employee id to choose whose work to show. The dedicated app must keep that rule.

## Requirements

- Sign in as an employee user whose role is STYLIST for one salon.
- Show only appointments and service lines assigned to that employee.
- Show the appointment date, scheduled time, service name, line status, and the customer information the existing appointment response already returns.
- Refresh the list, and retry when the API cannot be reached.
- Do not show rentals, bills, payments, tax, products, user administration, or other employees’ books.
- Do not let the app select another branch. Branch scope stays on the authenticated user.
- Do not grant service-line progress in this workstream unless a later assignment explicitly restores `appointments.own.progress` on STYLIST. Today that permission is not granted to STYLIST. Reception and supervisors progress lines with `appointments.lines.progress`.
- Do not accept a client-supplied employee id as the source of “my work”.

## Proposed architecture

- A new Flutter application, separate from `salon_booking_mobile` and `salon_customer_app`.
- The same development API (`http://localhost:5041` in development) and the same JWT login.
- One screen after sign-in: the assigned book, backed by `GET /api/Appointments/mine`.
- Session storage for the access token so the book is still there after the app is closed and opened again, until the token expires or the stylist signs out.
- No local appointment database and no offline writes in the first version.
- Authorization stays on the API. The app hides actions the token cannot perform; it does not become the enforcement point.

## Explicitly out of scope until a later assignment

- Starting or completing a service line from the stylist app.
- Rental asset management.
- Billing, payments, or feedback collection.
- A new database migration or a new stylist-only permission.
- Production deployment.

## Current stand-in

Until this workstream starts, stylists use My Work in the staff app. That screen lists assigned lines, shows schedule and status, and does not offer progress controls.
