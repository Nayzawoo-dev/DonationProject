# DEVELOPMENT_PROGRESS.md
## Danation — Charity/Donation Management Platform

> This file tracks the current implementation status so that future AI sessions
> can continue without relying on previous chat memory.
> **Always read PROJECT_SPEC.md first, then this file, then inspect source code.**

---

## Current Phase

**Phase 5: Full AJAX-First Refactoring Complete (Production-Ready)**

The entire web application user experience has been refactored to be AJAX-first, eliminating unnecessary full-page reloads and delivering a smooth, responsive, single-page-like experience across all key user and admin workflows.

---

## Last Build Status

**BUILD SUCCEEDED — 0 Errors, 0 Warnings**
Date: 2026-08-26
AJAX-First Refactoring Summary:
1. **Global JavaScript Architecture (`wwwroot/js/site.js`)**:
   - Added `Danation.loadPartial(url, container, onSuccess, onError)` for zero-page-reload content updates.
   - Added `Danation.getCsrfToken()` and automatic `X-Requested-With: XMLHttpRequest` header injection.
   - Enhanced `Danation.upload()` with robust anti-forgery injection and error notifications.
2. **Authentication Flow (Register, VerifyOtp, Login)**:
   - Updated `LoginService.LoginAsync` to return role for instant client-side redirection.
   - Converted `AccountController` actions (`Register`, `VerifyOtp`, `Login`) to dual-mode: returning structured JSON with redirect URLs for AJAX requests.
   - Refactored `Login.cshtml`, `Register.cshtml`, and `VerifyOtp.cshtml` to submit via jQuery AJAX with Notiflix feedback and smooth transitions.
3. **Public Campaign Discovery (`/Campaign/Index`)**:
   - Extracted `_CampaignListPartial.cshtml` containing active filter tags, campaign cards grid, and pagination.
   - Refactored `Views/Campaign/Index.cshtml` with live debounced search, township filter, status dropdown, and pagination.
   - Added `history.pushState` and `window.onpopstate` listener for seamless browser back/forward navigation.
4. **Donation Submissions (`/Donation/Submit`)**:
   - Updated `DonationController.Submit` to return JSON on AJAX POST.
   - Refactored `Views/Donation/Submit.cshtml` to upload transfer screenshot via AJAX `FormData` with progress indicators.
5. **Admin Portal Management (`/Admin/Campaigns`, `/Admin/Donations`, `/Admin/Users`, `/Admin/CreateCompletion`)**:
   - Extracted `_AdminCampaignTablePartial.cshtml`, `_AdminDonationTablePartial.cshtml`, `_AdminUserTablePartial.cshtml`.
   - Converted table filters (status tabs and live search debounce) to load partials dynamically without page refresh.
   - Converted `CreateCompletion` multi-image upload form to AJAX `FormData` upload.
6. **Notifications (`/Notification/Index`)**:
   - Added single-notification item click mark-as-read via AJAX `POST /Notification/MarkRead/{id}`.

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
