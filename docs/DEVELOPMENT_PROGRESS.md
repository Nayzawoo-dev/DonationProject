# DEVELOPMENT_PROGRESS.md
## Danation — Charity/Donation Management Platform

> This file tracks the current implementation status so that future AI sessions
> can continue without relying on previous chat memory.
> **Always read PROJECT_SPEC.md first, then this file, then inspect source code.**

---

## Current Phase

**Phase 2: Views Implementation**

Backend (Controllers + Services + ViewModels) is complete and building successfully.
All major Views are missing and must be implemented.

---

## Last Build Status

**BUILD SUCCEEDED — 0 Errors, 0 Warnings**
Date: 2026-08-25
Fixes applied this session:
1. Removed redundant private inner class `AdminCampaignSummaryViewModel` in `CampaignService.cs` (CS0050 error)
2. Added `using Microsoft.EntityFrameworkCore;` to: AdminController, NotificationService, LoginServices, UserService, DonationService
3. Added `using DonationEntity = DatabaseClass.Models.Donation;` alias to DonationService
4. Changed `new Donation {}` to `new DonationEntity {}` in DonationService.SubmitDonationAsync

---

## Last Test Status

Not yet tested (no Views exist to render UI; build compiles successfully).

---

## Completed

### Infrastructure & Architecture
- [x] Solution structure (two projects: Danation + DatabaseClass)
- [x] DatabaseClass project: All 8 entities + AppDbContext (EF Core scaffold)
- [x] ServiceCollectionExtension: DbContext, MemoryCache, FluentEmail, Cookie Auth, Rate Limiter, DI registrations
- [x] Program.cs: middleware pipeline (auth, routing, static files, rate limiter)
- [x] appsettings.json: ConnectionStrings, EmailSettings, DonationPayment

### Entities (DatabaseClass/Models/)
- [x] User.cs
- [x] Campaign.cs
- [x] CampaignImage.cs
- [x] CampaignDocument.cs
- [x] Donation.cs
- [x] CampaignCompletion.cs
- [x] CompletionImage.cs
- [x] Notification.cs
- [x] AppDbContext.cs (full relationships, constraints, defaults)

### ViewModels (Danation/ViewModels/)
- [x] AccountViewModels: LoginViewModel, RegisterViewModel, VerifyOtpViewModel
- [x] ProfileViewModels: UserProfileViewModel, EditProfileViewModel, ChangePasswordViewModel
- [x] CampaignViewModels: CampaignListItemViewModel, CampaignListViewModel, CampaignDetailViewModel,
      CampaignImageViewModel, CampaignDocumentViewModel, CampaignCompletionViewModel,
      CompletionImageViewModel, CreateCampaignViewModel, EditCampaignViewModel, MyCampaignsViewModel
- [x] DonationViewModels: DonationSubmitViewModel, DonationHistoryItemViewModel, MyDonationsViewModel
- [x] AdminViewModels: AdminDashboardViewModel, AdminCampaignSummaryViewModel, AdminDonationSummaryViewModel,
      AdminApproveDonationViewModel, AdminUserViewModel, AdminCreateCompletionViewModel
- [x] NotificationViewModels: NotificationViewModel (with RelativeTime), NotificationListViewModel

### Services (Danation/Services/)
- [x] UserService: RegisterAsync, GenerateAndSendOtpAsync, VerifyOtpAsync, ResendOtpAsync,
      GetUserProfileAsync, GetEditProfileViewModelAsync, UpdateProfileAsync, ChangePasswordAsync
- [x] LoginServices (nested LoginService): LoginAsync, SignInUserAsync, LogoutAsync
- [x] EmailService: SendEmailAsync (FluentEmail wrapper)
- [x] FileService: ValidateImageFile, ValidateDocumentFile, SaveImageAsync, DeleteFile
- [x] CampaignService: GetPublicCampaignsAsync, GetDetailAsync, GetDetailWithDocsAsync, CreateAsync,
      GetEditViewModelAsync, UpdateAsync, DeleteAsync, UploadImageAsync, DeleteImageAsync,
      UploadDocumentAsync, DeleteDocumentAsync, GetUserCampaignsAsync,
      ApproveCampaignAsync, RejectCampaignAsync, CloseCampaignAsync, GetAdminCampaignsAsync
- [x] DonationService: SubmitDonationAsync, GetMyDonationsAsync, GetAdminDonationsAsync,
      ApproveDonationAsync (with transaction + goal check), RejectDonationAsync, GetDonationDetailAsync
- [x] NotificationService: CreateAsync, GetUnreadCountAsync, GetLatestAsync, GetAllAsync,
      MarkReadAsync, MarkAllReadAsync

### Controllers (Danation/Controllers/)
- [x] AccountController: Register, VerifyOtp, ResendOtp, Login, Logout, AccessDenied
- [x] ProfileController: Index, Edit, ChangePassword
- [x] CampaignController: Index, Detail, My, Create, Edit, Delete, UploadImage, DeleteImage,
      UploadDocument, DeleteDocument
- [x] DonationController: Submit (GET+POST), My
- [x] AdminController: Dashboard, Campaigns, ApproveCampaign, RejectCampaign, CloseCampaign,
      Donations, ApproveDonation, RejectDonation, CreateCompletion (GET+POST), Users, ToggleUserActive,
      CampaignDetail
- [x] NotificationController: Index, UnreadCount, Latest, MarkRead, MarkAllRead
- [x] HomeController: Index, Privacy, Error

---

## In Progress

- [ ] Views implementation (ALL views missing — see Pending section below)
- [ ] Layout (_Layout.cshtml) — exists but minimal/incomplete (no auth nav, no notifications)

---

## Pending

### Views — HIGH PRIORITY (entire UI is missing)

#### Shared/_Layout.cshtml — NEEDS COMPLETE REWRITE
Current state: Basic Bootstrap navbar with no auth nav, no notification bell, no user menu.
Required: Professional charity platform layout with:
- Navbar: Logo, Campaign listing link, Login/Register (guest) OR User menu + notifications + My Campaigns (user)
- Admin separate layout or admin-aware nav
- Notification bell with unread count (AJAX polling)
- User profile image in nav
- Footer with project info
- Anti-forgery token for AJAX
- Notiflix initialization

#### Account Views
- [ ] Views/Account/Login.cshtml
- [ ] Views/Account/Register.cshtml
- [ ] Views/Account/VerifyOtp.cshtml
- [ ] Views/Account/AccessDenied.cshtml

#### Profile Views
- [ ] Views/Profile/Index.cshtml (profile display with stats)
- [ ] Views/Profile/Edit.cshtml (edit form with AJAX + image upload)

#### Campaign Views
- [ ] Views/Campaign/Index.cshtml (public grid with search/filter/pagination)
- [ ] Views/Campaign/Detail.cshtml (detail page with progress bar, donate button, completion section)
- [ ] Views/Campaign/My.cshtml (user's own campaigns list)
- [ ] Views/Campaign/Create.cshtml (create form, AJAX submit)
- [ ] Views/Campaign/Edit.cshtml (edit form + image management + document management, all AJAX)

#### Donation Views
- [ ] Views/Donation/Submit.cshtml (payment info + screenshot upload)
- [ ] Views/Donation/My.cshtml (donation history with status badges)

#### Notification Views
- [ ] Views/Notification/Index.cshtml (all notifications with mark-read)

#### Admin Views
- [ ] Views/Admin/Dashboard.cshtml (stats cards + recent tables)
- [ ] Views/Admin/Campaigns.cshtml (table with approve/reject/close actions)
- [ ] Views/Admin/Donations.cshtml (table with approve/reject + screenshot viewer)
- [ ] Views/Admin/Users.cshtml (user table with toggle active)
- [ ] Views/Admin/CampaignDetail.cshtml (full campaign view with documents)
- [ ] Views/Admin/CreateCompletion.cshtml (create completion + image upload)

#### Home Views
- [ ] Views/Home/Index.cshtml — NEEDS rewrite (currently default ASP.NET template text)

#### CSS/JS
- [ ] wwwroot/css/site.css — currently near-empty (only 667 bytes)
- [ ] wwwroot/js/site.js — currently near-empty (only 231 bytes)

### Features Pending (after Views)
- [ ] Profile image upload (AJAX)
- [ ] AJAX anti-forgery setup in layout/site.js
- [ ] Notification bell AJAX polling in layout
- [ ] Campaign image management AJAX (in Edit view)
- [ ] Campaign document management AJAX (in Edit view)
- [ ] Admin donation screenshot viewer
- [ ] Completion image upload (in CreateCompletion view)
- [ ] Admin campaign documents review page

---

## Known Issues

1. **All views are missing** — the application cannot render any page except the default home page
2. **_Layout.cshtml** is the default ASP.NET template (no auth nav, no notifications, no user context)
3. **Home/Index.cshtml** still has the default ASP.NET template content
4. **wwwroot/css/site.css** is near-empty (no custom styles)
5. **wwwroot/js/site.js** is near-empty (no AJAX helpers, no Notiflix setup)
6. **Connection string** is hardcoded in AppDbContext.cs (warning CS1030) — harmless but should be removed from source eventually
7. No pagination component partial view
8. No error partial views

---

## Important Decisions

1. **Namespace conflict**: Project namespace `Donation` conflicts with entity `DatabaseClass.Models.Donation`.
   Resolution: Add type alias `using DonationEntity = DatabaseClass.Models.Donation;` in DonationService.
   All service files need explicit `using Microsoft.EntityFrameworkCore;`.

2. **LoginService is a nested class**: `LoginServices.cs` contains outer class `LoginServices` with inner `LoginService`.
   Registered as `LoginService` in DI. Referenced as `static Donation.Services.LoginServices`.

3. **OTP storage**: IMemoryCache only (key: `OTP_{email.lower}`). Never in SQL.

4. **Payment info**: appsettings.json `DonationPayment` section. Never in SQL.

5. **Document access control**: Campaign documents (`/uploads/documents/`) are image-based but should not be
   publicly exposed without authorization. Admin-facing only.

6. **No migrations**: The database appears to have been scaffolded from an existing SQL Server database
   (EF Core scaffold). No migrations folder exists. Schema changes must be done via SQL directly.

---

## Next Task (for next session)

**START HERE**: Implement Views in this order:

1. **_Layout.cshtml** (Shared) — complete rewrite with proper auth nav + notification bell
2. **Home/Index.cshtml** — professional charity landing page
3. **Account views** (Login, Register, VerifyOtp, AccessDenied)
4. **Profile views** (Index, Edit with AJAX)
5. **Campaign/Index.cshtml** (public campaign grid)
6. **Campaign/Detail.cshtml** (campaign detail with donate button)
7. **Campaign/My.cshtml** (user's campaigns)
8. **Campaign/Create.cshtml** and **Campaign/Edit.cshtml** (with AJAX image/doc management)
9. **Donation/Submit.cshtml** and **Donation/My.cshtml**
10. **Notification/Index.cshtml**
11. **Admin views** (Dashboard, Campaigns, Donations, Users, CampaignDetail, CreateCompletion)
12. **CSS** (site.css) and **JS** (site.js — AJAX helpers, anti-forgery setup)

---

## Last Updated

2026-08-25 — Session 1
- Full project audit completed
- Build errors fixed (CS0050, EF Core using directives, namespace alias)
- Build: SUCCESS (0 errors, 0 warnings)
- Documentation created
