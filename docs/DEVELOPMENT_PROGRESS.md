# DEVELOPMENT_PROGRESS.md
## Danation — Charity/Donation Management Platform

> This file tracks the current implementation status so that future AI sessions
> can continue without relying on previous chat memory.
> **Always read PROJECT_SPEC.md first, then this file, then inspect source code.**

---

## Current Phase

**Phase 6: Full Real-Time SignalR Architecture Complete (Production-Ready)**

Real-time capabilities have been seamlessly integrated on top of the existing ASP.NET Core MVC, EF Core, and jQuery AJAX architecture using ASP.NET Core SignalR. All business rules, security models, and database-first designs are preserved with zero full-page reloads and targeted DOM updates.

---

## Last Build Status

**BUILD SUCCEEDED — 0 Errors, 0 Warnings**
Date: 2026-09-03
SignalR Real-Time Implementation Summary:
1. **Centralized Hub Architecture (`Danation/Hubs/AppHub.cs` mapped to `/hubs/app`)**:
   - Automatic `Admins` group assignment in `OnConnectedAsync` for role `ADMIN`.
   - `JoinCampaign(campaignId)` and `LeaveCampaign(campaignId)` for granular group scoping.
   - User-targeted delivery via `Clients.User(userId)` leveraging `ClaimTypes.NameIdentifier` across multiple tabs and devices.
2. **Service-Layer Event Dispatchers (`IHubContext<AppHub>`)**:
   - `NotificationService`: Real-time push via `ReceiveNotification` and multi-tab read synchronization via `NotificationReadUpdated`.
   - `CampaignService`: Real-time `CampaignCreated` alerts to admins, and `CampaignStatusChanged` to all affected clients on approval, rejection, and closing.
   - `DonationService`: Real-time `DonationCreated` alerts to admins, live progress updates via `CampaignDonationUpdated`, donor notifications via `DonationStatusChanged`, and live counter sync via `AdminDashboardStats`.
   - `AdminController`: Broadcasts `CampaignStatusChanged` with `COMPLETED` upon campaign completion creation.
3. **Client-Side Real-Time Management (`wwwroot/js/signalr-client.js`)**:
   - Built with official `@microsoft/signalr` library with exponential backoff automatic reconnection (`[0, 2000, 5000, 10000, 30000]`).
   - Graceful offline fallback: failure never breaks AJAX forms or UI actions.
   - Instant Notiflix toast notifications, navbar unread badge counters, and real-time dropdown prepending.
4. **Targeted Real-Time UI Reactivity**:
   - **Campaign Detail (`/Campaign/Detail/{id}`)**: Live raised amount, progress bar width, goal reached badge, and donate button toggle without page refresh.
   - **Public Campaign Grid (`/Campaign/Index`)**: In-place card progress and status badge updates.
   - **Admin Dashboard (`/Admin/Dashboard`)**: Live stat cards (Total Users, Pending Campaigns, Open Campaigns, Pending Donations, Total Approved MMK) and instant pending item list prepend.
   - **Admin Management Tables (`/Admin/Campaigns`, `/Admin/Donations`)**: Instant status badge and action button swaps.
   - **User Portals (`/Campaign/My`, `/Donation/My`, `/Notification/Index`)**: Live status updates, verified amount indicators, and smooth notification item prepending.

---

## Permanent Business Rules Enforced

1. **Campaign Edit Locking**: Once an Admin approves a Campaign and it becomes APPROVED/OPEN, the Campaign Owner cannot edit the Campaign. This restriction is strictly enforced server-side, not only through the UI (only PENDING campaigns can be user-edited).
2. **ContactPhone Privacy**: Campaign ContactPhone is PRIVATE. Only Admin can view it. Public users must never receive it through View, DTO, AJAX, API, JavaScript or hidden HTML. Enforced server-side.
3. **Self-Donation Protection**: Campaign owners are prevented server-side from donating to their own campaigns.
4. **Donation Transfer Phone**: Configured via `appsettings.json` (`DonationPayment` section) separate from admin phone numbers (no database table).
5. **Campaign Deletion**: Only PENDING campaigns can be deleted by owner (deletes files from disk).
6. **Campaign Total Calculation**: Sum of APPROVED donations only. Goal check occurs inside a database transaction during donation approval.

---

## Completed Features Matrix

### Infrastructure & Architecture
- [x] Solution structure (two projects: Danation + DatabaseClass)
- [x] DatabaseClass project: All 8 entities + AppDbContext
- [x] ServiceCollectionExtension: DbContext, MemoryCache, FluentEmail, Cookie Auth, Rate Limiter, DI registrations
- [x] Program.cs: middleware pipeline (auth, routing, static files, rate limiter)
- [x] appsettings.json: ConnectionStrings, EmailSettings, DonationPayment configuration

### Authentication & Account
- [x] Register with Email OTP (IMemoryCache, 5-min expiry, max 5 attempts, 60s resend throttle)
- [x] Login with Cookie Auth & Remember Me
- [x] Forgot Password flow (Email -> OTP -> Verify OTP -> Reset Token -> New Password -> Login)
- [x] Anti-enumeration security in Forgot Password
- [x] AccessDenied view

### User Profile & Public Profile
- [x] User Profile page with stats (total campaigns, total donations)
- [x] Edit Profile with AJAX + Profile Image upload (auto-delete old image, refresh auth cookie)
- [x] Change Password with current password verification
- [x] Public Profile (`/Profile/Public/{id}`) displaying: Profile image, Full name, Member since, Total campaigns created, Total campaigns donated, Created campaigns list, Supported campaigns list (strictly zero exposure of email, phone, donation amounts, or screenshots)

### Campaign Lifecycle & Location Features
- [x] Campaign creation with Title, GoalAmount, Description, Address, Township, ContactPhone (Status = PENDING)
- [x] Campaign editing (strictly locked to owner and PENDING status only; locked upon admin approval)
- [x] Campaign deletion (restricted to owner & PENDING status only; cascades image/document file deletions)
- [x] Campaign image & document management with AJAX (upload, gallery, deletion; locked when approved)
- [x] Public campaign listing with search, status filter, and Township filter + pagination
- [x] Campaign detail page with progress bar, goal status, organizer public profile link, and lightbox gallery
- [x] Privacy enforcement: `ContactPhone` is strictly excluded from all public ViewModels and public endpoints; only Admin can view it.

### Donation Workflow
- [x] External transfer via KPay/WavePay (transfer phone details read from `appsettings.json`, no DB table)
- [x] Donation submission with screenshot upload (Amount=null, Status=PENDING)
- [x] Server-side restriction preventing campaign owner from donating to their own campaign
- [x] Donor donation history page with status badges
- [x] Admin donation approval with verified amount entry + database transaction + automatic campaign goal check (`GOAL_REACHED`) + notifications
- [x] Admin donation rejection with reason + notifications (rejected records preserved)

### Campaign Completion (Admin)
- [x] Admin creation of campaign completion records for `CLOSED` campaigns with multi-image upload
- [x] Public display of completion evidence on campaign detail page

### Notifications
- [x] In-app notifications stored in database for approvals, rejections, goal reached, and completions
- [x] Notification dropdown with unread count badge + AJAX polling (60s interval)
- [x] Notification index page with mark-as-read and mark-all-read via AJAX (optimized with `ExecuteUpdateAsync` and pagination)

### Admin Portal
- [x] Admin Dashboard with platform metrics & recent pending items
- [x] Admin Campaign Management (approve, reject with modal reason, close campaign)
- [x] Admin Donation Management (approve with amount modal, reject with modal reason, screenshot lightbox)
- [x] Admin User Management (listing, statistics, toggle active/inactive via AJAX)
- [x] Admin Campaign Detail & Document Review (showing documents, images, township, address, and admin-only contact phone)

### Real-Time SignalR Engine
- [x] Centralized `AppHub` (`Danation/Hubs/AppHub.cs`) mapped to `/hubs/app`
- [x] Role-based groups: auto-join `Admins` group for admin users in `OnConnectedAsync`
- [x] User-targeted messaging using `Clients.User(userId)` mapping to `ClaimTypes.NameIdentifier` (multi-tab & multi-device sync)
- [x] Campaign-scoped groups (`Campaign_{id}`) with client `JoinCampaign` / `LeaveCampaign`
- [x] Real-time user notifications: instant bell badge counter update + Notiflix toast + dropdown list update
- [x] Real-time read sync: marking notification(s) read syncs unread badge across all open tabs
- [x] Real-time campaign lifecycle: status badge updates, edit lock enforcement, and donate button toggle
- [x] Real-time donation progress: instant raised amount and progress bar updates on public detail page and public discovery cards
- [x] Real-time admin dashboard live sync: pending counters, approved totals, and live pending queue updates
- [x] Robust client resilience: automatic reconnect with backoff (`[0, 2s, 5s, 10s, 30s]`), zero disruption to AJAX CRUD on network disconnect

### Frontend & Styling
- [x] Layout (`_Layout.cshtml`) with responsive navbar, user menu, notification bell, anti-forgery AJAX setup
- [x] Complete custom design system in `wwwroot/css/site.css`
- [x] Global AJAX helper functions, anti-forgery headers, and Notiflix configurations in `wwwroot/js/site.js`
- [x] Landing page (`Views/Home/Index.cshtml`) with hero section, platform stats, featured campaigns, and impact highlights

---

## Last Updated

2026-08-26
- Final Project Polish: Performance optimizations, EF Core `ExecuteUpdateAsync`, IMemoryCache caching, responsive table wrappers, and AJAX token safety.
- Build: SUCCESS (0 errors, 0 warnings)
