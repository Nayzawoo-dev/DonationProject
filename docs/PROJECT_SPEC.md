# Danation — Charity/Donation Management Platform
## PROJECT_SPEC.md — Permanent Source of Truth

> **Instructions for any AI agent resuming this project:**
> 1. Read this file first.
> 2. Read `DEVELOPMENT_PROGRESS.md` next.
> 3. Inspect actual source code to verify current state.
> 4. Build the project and check errors.
> 5. Continue from the next pending task in DEVELOPMENT_PROGRESS.md.
> **Do NOT restart from scratch. Do NOT create duplicate architecture.**

---

## 1. Project Purpose

Danation is a professional, secure Charity/Donation Management Web Application.

- **Users**: Register, create charity campaigns, manage campaign media, donate to approved campaigns.
- **Admins**: Review/approve/reject campaigns and donations; manage campaign completion; moderate the platform.

This is **NOT** a payment gateway. Donors transfer money externally (KPay/WavePay) and upload a screenshot as proof. Admins verify and record the actual amount.

---

## 2. Technology Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC (.NET 10) |
| Language | C# |
| ORM | Entity Framework Core 10 |
| Database | Microsoft SQL Server |
| Views | Razor (.cshtml) |
| CSS Framework | Bootstrap 5.3 |
| JavaScript | jQuery 3.7 |
| AJAX | jQuery $.ajax() |
| UI Notifications | Notiflix 3.2.6 |
| Admin Tables | DataTables 1.13.6 + Bootstrap5 |
| Email | FluentEmail 3.0.2 + Gmail SMTP |
| Caching | IMemoryCache (OTP only) |
| Auth | Cookie Authentication (ASP.NET Core) |
| Password Hashing | ASP.NET Core Identity PasswordHasher |
| DI Container | ASP.NET Core built-in |
| Rate Limiting | ASP.NET Core built-in RateLimiter |

---

## 3. Solution Structure

```
DonationProject/
  Danation/                   <- Main web project (namespace: Donation)
    Controllers/
      AccountController.cs
      AdminController.cs
      CampaignController.cs
      DonationController.cs
      HomeController.cs
      NotificationController.cs
      ProfileController.cs
    Services/
      CampaignService.cs
      DonationService.cs
      EmailService.cs
      FileService.cs
      LoginServices.cs         <- Contains nested class LoginService
      NotificationService.cs
      UserService.cs
    Extension/
      ServiceCollectionExtension.cs
    ViewModels/
      Account/AccountViewModels.cs
      Admin/AdminViewModels.cs
      Campaign/CampaignViewModels.cs
      Donation/DonationViewModels.cs
      Notification/NotificationViewModels.cs
      Profile/ProfileViewModels.cs
    Views/
      Home/Index.cshtml, Privacy.cshtml
      Shared/_Layout.cshtml, Error.cshtml
      Account/             <- NEEDS views (Login, Register, VerifyOtp, AccessDenied)
      Admin/               <- NEEDS views (Dashboard, Campaigns, Donations, Users, etc.)
      Campaign/            <- NEEDS views (Index, Detail, My, Create, Edit)
      Donation/            <- NEEDS views (Submit, My)
      Notification/        <- NEEDS views (Index)
      Profile/             <- NEEDS views (Index, Edit)
    Models/ErrorViewModel.cs
    wwwroot/css/site.css, js/site.js, uploads/ (runtime)
    appsettings.json
    Program.cs
  DatabaseClass/              <- EF Core entities + DbContext
    Models/
      AppDbContext.cs
      Campaign.cs, CampaignCompletion.cs, CampaignDocument.cs
      CampaignImage.cs, CompletionImage.cs
      Donation.cs, Notification.cs, User.cs
```

### CRITICAL NAMESPACE ISSUE
The main project namespace is `Donation`. The entity `DatabaseClass.Models.Donation` conflicts.
- In `DonationService.cs`: `using DonationEntity = DatabaseClass.Models.Donation;` then use `new DonationEntity {}`
- All service files MUST explicitly include: `using Microsoft.EntityFrameworkCore;`

---

## 4. Database Entities

### Users
- Id, FullName(150), Username(50 UNIQUE), Email(150 UNIQUE), Phone(30?)
- PasswordHash(255), ProfileImage(500?), Role varchar(20), EmailVerified bit
- IsActive bit, CreatedAt datetime2, UpdatedAt datetime2?

### Campaigns
- Id, UserId FK->Users, Title(200), Description(nvarchar max)
- GoalAmount decimal(18,2), Status varchar(30) [PENDING/OPEN/GOAL_REACHED/CLOSED/COMPLETED/REJECTED]
- CreatedAt, UpdatedAt?, ClosedAt?, CompletedAt?
- **NO StartDate or EndDate**

### CampaignImages
- Id, CampaignId FK->Campaigns, ImageUrl(500), Caption(500?), CreatedAt

### CampaignDocuments
- Id, CampaignId FK->Campaigns, ImageUrl(500), DocumentType(100?), CreatedAt

### Donations
- Id, CampaignId FK->Campaigns, DonorId FK->Users
- Amount decimal(18,2)? (null until admin verifies), TransferScreenshot(500)
- Status varchar(20) [PENDING/APPROVED/REJECTED]
- VerifiedBy int? FK->Users, VerifiedAt datetime2?, CreatedAt

### CampaignCompletions
- Id, CampaignId FK->Campaigns (UNIQUE), Caption(nvarchar max)
- CreatedBy int FK->Users (admin), CreatedAt

### CompletionImages
- Id, CompletionId FK->CampaignCompletions, ImageUrl(500), Caption(500?), CreatedAt

### Notifications
- Id, UserId FK->Users, Title(200), Message(1000), IsRead bit, CreatedAt

---

## 5. Entity Relationships

```
User (1) -- (many) Campaign
User (1) -- (many) Donation [DonorId]
User (1) -- (many) Donation [VerifiedBy]
User (1) -- (many) CampaignCompletion [CreatedBy]
User (1) -- (many) Notification
Campaign (1) -- (many) CampaignImage
Campaign (1) -- (many) CampaignDocument
Campaign (1) -- (many) Donation
Campaign (1) -- (0..1) CampaignCompletion
CampaignCompletion (1) -- (many) CompletionImage
```

---

## 6. Authentication & Authorization

- Scheme: Cookie Auth, cookie name: `Danation.Auth`
- Login: /Account/Login | AccessDenied: /Account/AccessDenied
- Roles: USER, ADMIN
- Claims: NameIdentifier(userId), Name(FullName), Email, Role, RememberMe, ProfileImage
- Admin routes: `[Authorize(Roles = "ADMIN")]`
- User routes: `[Authorize]`
- Cookie expiry: 5 days (persistent) or 8 hours (session)

---

## 7. Registration & OTP Flow

```
POST /Account/Register
  Validate form -> Check email/username uniqueness
  Create User (Role=USER, EmailVerified=false, IsActive=false)
  GenerateSecureOtp() [RandomNumberGenerator, 6 digits]
  Store in IMemoryCache: "OTP_{email.lower}"
    { Code, ExpiresAt=UTC+5min, Attempts=0, LastSentAt }
  Send OTP via FluentEmail (Gmail SMTP)
  Redirect to /Account/VerifyOtp?email=...

POST /Account/VerifyOtp
  Read OTP from cache -> check expiry -> increment attempts (max 5)
  If correct: EmailVerified=true, IsActive=true, remove from cache
  Redirect to /Account/Login

POST /Account/ResendOtp (AJAX)
  Throttle: 60 seconds between sends
  Generate new OTP, replace in cache, resend
```

**OTP Rules**: Never in DB, never logged, never in JS/HTML, max 5 attempts, single-use, UTC expiry.

---

## 8. User Profile

- GET /Profile -> own profile (UserProfileViewModel)
- GET+POST /Profile/Edit -> EditProfileViewModel (FullName, Phone, ProfileImage)
  - Ownership check: `model.UserId == currentUserId` server-side
  - Profile image: max 2MB, /uploads/profiles/, delete old on update
- POST /Profile/ChangePassword (AJAX)
- After update: refresh auth cookie via SignInUserAsync

---

## 9. Campaign Lifecycle

```
PENDING -> (Admin approves) -> OPEN
OPEN -> (approved donations >= GoalAmount) -> GOAL_REACHED [auto at donation approval]
GOAL_REACHED -> (Admin closes) -> CLOSED
CLOSED -> (Admin creates completion) -> COMPLETED
PENDING -> (Admin rejects) -> REJECTED
```

Rules:
- Owner can ONLY edit PENDING campaigns (Once approved/OPEN, locked from editing)
- Only PENDING campaigns can be deleted by owner (also removes files)
- Admin approves/rejects PENDING only; closes GOAL_REACHED only
- New donations blocked when status != OPEN

---

## 10. File Upload Rules

| Purpose | Folder | Max Size | Extensions |
|---|---|---|---|
| Profile images | /uploads/profiles/ | 2MB | jpg jpeg png webp gif |
| Campaign images | /uploads/campaigns/ | 5MB | jpg jpeg png webp gif |
| Campaign documents | /uploads/documents/ | 10MB | jpg jpeg png webp gif |
| Transfer screenshots | /uploads/donations/ | 5MB | jpg jpeg png webp gif |
| Completion evidence | /uploads/completions/ | 5MB | jpg jpeg png webp gif |

Always: validate extension, safe filename (Guid), no path traversal, never trust original filename.

---

## 11. Donation Workflow

```
GET /Donation/Submit/{id}
  Auth required, campaign must be OPEN, donor != campaign owner (server-side)
  Show payment info from appsettings.json [DonationPayment section]

POST /Donation/Submit
  Validate screenshot -> Create Donation (Amount=null, Status=PENDING)

Admin POST /Admin/ApproveDonation
  Admin enters verified amount
  Donation: Status=APPROVED, Amount=verifiedAmount, VerifiedBy, VerifiedAt
  [Transaction] Check if campaign total >= GoalAmount -> GOAL_REACHED
  Notify donor

Admin POST /Admin/RejectDonation
  Donation: Status=REJECTED (NEVER deleted)
  Notify donor
```

Payment config (appsettings.json, NOT database):
```json
"DonationPayment": { "Method": "KPay", "PhoneNumber": "...", "AccountName": "..." }
```

---

## 12. Campaign Completion (Admin Only)

- Only for CLOSED campaigns; one record per campaign (UNIQUE index)
- Creates CampaignCompletion + CompletionImages; sets Campaign.Status=COMPLETED
- Donors can view evidence on campaign detail page

---

## 13. Notifications (Notification Table)

Events: Campaign approved/rejected, donation approved/rejected, goal reached, campaign closed/completed.
API: GET /Notification, GET /Notification/UnreadCount (AJAX poll), GET /Notification/Latest (AJAX, 8 items),
POST /Notification/MarkRead/{id}, POST /Notification/MarkAllRead.

---

## 14. AJAX Requirements

ALL CRUD via jQuery $.ajax(). Every request must:
1. Include anti-forgery token header
2. Show Notiflix loading, success/error
3. Prevent duplicate submission
4. Return JSON: `{ success: bool, message: string, data?: object }`
5. Update only affected DOM (no full reload)

File uploads: use FormData, do NOT set Content-Type manually.
Anti-forgery in layout: `$.ajaxSetup({ headers: { 'RequestVerificationToken': ... } })`

---

## 15. Security Rules

- `[Authorize(Roles = "ADMIN")]` on all admin routes
- Server-side ownership verification on all user resources
- Anti-forgery tokens on all POST forms and AJAX
- PasswordHasher<User> for all passwords
- Rate limiting: 30/min guests, 100/min users, 1000/min admins
- No stack traces to users; proper HTTP status codes on errors
- Never trust client-side role/ID/ownership values

---

## 16. Performance Rules

- All DB queries: async/await + AsNoTracking() for reads
- Use IQueryable + Select() projection; avoid N+1 queries
- Pagination: 9 campaigns/page on public listing
- Server-side filtering and sorting
- Transactions for donation approval + campaign status
- IMemoryCache for OTP only

---

## 17. Admin Account Creation

Direct SQL only — no public admin registration:
```sql
INSERT INTO Users (FullName, Username, Email, PasswordHash, Role, EmailVerified, IsActive, CreatedAt)
VALUES ('Admin', 'admin', 'admin@email.com', '<BCrypt-hash>', 'ADMIN', 1, 1, SYSDATETIME())
```
Password hash must be generated using ASP.NET Core Identity PasswordHasher<User>.

---

## 18. Important Business Rules

1. Donors cannot donate to own campaign (server-side enforced)
2. Only PENDING campaigns can be deleted
3. Once an Admin approves a Campaign and it becomes APPROVED/OPEN, the Campaign Owner cannot edit the Campaign. This restriction must be enforced server-side, not only through the UI (only PENDING campaigns can be user-edited).
4. Rejected donations are NEVER deleted
5. Campaign total = SUM of APPROVED donation amounts only
6. Goal check happens inside a transaction at donation approval time
7. One completion per campaign (UNIQUE DB constraint)
8. Only CLOSED campaigns can be marked completed
9. Only GOAL_REACHED campaigns can be closed by admin
10. No admin registration through public form
11. OTP is single-use; max 5 attempts; 60s resend throttle
