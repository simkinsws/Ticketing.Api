# Ticketing API - Backend

A modern ASP.NET Core 8 REST API for managing support tickets with authentication, token-based authorization, and email notifications.

## 🎯 Features

- **User Management**: Registration, email confirmation, password reset
- **Authentication**: JWT tokens with refresh token rotation
- **Ticket System**: Create, update, and track support tickets
- **Comments**: Add comments/replies to tickets with author tracking
- **Role-Based Access**: Admin and Customer roles
- **Email Notifications**: Confirmation and password reset emails via SendGrid
- **Cookie-Based Auth**: Secure HttpOnly cookies for token storage

## 📋 Tech Stack

- **Framework**: ASP.NET Core 8
- **Database**: SQL Server with Entity Framework Core
- **Authentication**: JWT Bearer + Identity
- **Email**: SendGrid API
- **Database Migrations**: EF Core Migrations

## 🔄 API Flow

```
User Registration
    ↓
Email Confirmation
    ↓
Login (Get JWT + Refresh Tokens)
    ↓
Create/View Tickets
    ↓
Add Comments
    ↓
Token Refresh (Auto-renewal)
    ↓
Logout
```

## 🔐 Authentication Flow

```
POST /auth/login
    ↓
Backend issues:
├─ Access Token (15 min) in "at" cookie
└─ Refresh Token (30 days) in "rt" cookie
    ↓
Frontend: All API requests send cookies automatically
    ↓
Token expires? Auto-refresh via /auth/refresh endpoint
    ↓
Logout: Both cookies deleted
```

## 📁 Key Endpoints

### Authentication
```
POST   /auth/register           - Create account
POST   /auth/confirm-email      - Verify email
POST   /auth/login              - Get tokens
POST   /auth/refresh            - Renew access token
POST   /auth/logout             - Clear tokens
POST   /auth/forgot-password    - Request password reset
POST   /auth/reset-password     - Complete password reset
GET    /auth/me                 - Get current user profile
```

### Tickets
```
POST   /tickets                 - Create ticket
GET    /tickets/my              - Get user's tickets
GET    /tickets/{id}            - Get ticket with comments
PUT    /tickets/{id}            - Update ticket
POST   /tickets/{id}/comments   - Add comment
```

### Admin
```
GET    /admin/tickets           - Get all tickets (filter by userId, status, category)
PATCH  /admin/tickets/{id}/status - Update ticket status
DELETE /admin/tickets/{id}      - Delete ticket
DELETE /admin/tickets/user/{userId} - Delete user's tickets
GET    /admin/users             - Get all users
```

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or Express)
- SendGrid API key

### Configuration

1. **Update `appsettings.json`**:

```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\mssqllocaldb;Database=TicketingDb;Trusted_Connection=True;"
  },
  "Jwt": {
    "Issuer": "Ticketing.Api",
    "Audience": "Ticketing.Web",
    "Key": "your-super-secret-key-at-least-32-chars",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 30
  },
  "SendGridSettings": {
    "ApiKey": "your-sendgrid-api-key",
    "FromEmail": "noreply@ticketing.com",
    "FromName": "Ticketing System"
  },
  "EmailConfirmation": {
    "BaseUrl": "http://localhost:5173",
    "ExpirationMinutes": 1440
  }
}
```

2. **Apply Migrations**:
```bash
dotnet ef database update
```

3. **Run the API**:
```bash
dotnet run
```

API runs on: `https://localhost:5001` or `http://localhost:5000`

## 🗄️ Database Schema

**Core Tables**:
- `AspNetUsers` - User accounts
- `AspNetRoles` - User roles (Admin, Customer)
- `Tickets` - Support tickets
- `TicketComments` - Ticket replies
- `RefreshTokens` - Token rotation tracking

**Relationships**:
```
AspNetUsers (1) ----→ (∞) Tickets
AspNetUsers (1) ----→ (∞) TicketComments
Tickets (1) ----→ (∞) TicketComments
```

## 🔒 Security Features

- ✅ JWT Bearer authentication
- ✅ Refresh token rotation (revoked after use)
- ✅ HttpOnly cookies (XSS protection)
- ✅ Password hashing with Identity
- ✅ Role-based authorization
- ✅ Email confirmation required
- ✅ Account lockout on failed logins
- ✅ CORS configured for frontend

## 📚 Project Structure

```
Controllers/
├── AuthController.cs       - Authentication endpoints
├── TicketsController.cs    - Ticket management
└── AdminController.cs      - Admin operations

Domain/
├── ApplicationUser.cs      - User model
├── Ticket.cs              - Ticket model
└── TicketComment.cs       - Comment model

DTOs/
├── AuthDtos.cs            - Auth requests/responses
├── TicketDtos.cs          - Ticket requests/responses
└── AdminDtos.cs           - Admin requests/responses

Services/
├── TokenService.cs        - JWT token generation
├── EmailService.cs        - Email sending via SendGrid
└── JwtOptions.cs          - JWT configuration

Data/
├── AppDbContext.cs        - Database context
└── Configuration/
    └── ModelBuilderExtensions.cs - Entity configuration
```

## 🔄 Token Management

**Access Token** (JWT):
- 15 minutes expiration
- Stored in HttpOnly cookie named `at`
- Used for API authentication
- Cannot be read by JavaScript (XSS safe)

**Refresh Token**:
- 30 days expiration
- Stored in HttpOnly cookie named `rt`
- Used ONLY to get new access token
- One-time use (revoked after refresh)

## 📧 Email Configuration

Requires SendGrid API key:
1. Create SendGrid account
2. Get API key
3. Add to `appsettings.json`
4. Confirmation emails sent on registration
5. Reset emails sent on password reset

## 🧪 Testing with Swagger

API includes Swagger/OpenAPI documentation:
1. Run the API
2. Open: `https://localhost:5001/swagger`
3. Authorize with JWT token
4. Test all endpoints

## 🔧 Environment Variables (Production)

For production, use environment variables instead of appsettings.json:

```bash
Jwt__Key=your-secret-key
SendGridSettings__ApiKey=your-sendgrid-key
ConnectionStrings__Default=your-connection-string
EmailConfirmation__BaseUrl=https://yourdomain.com
```

## 📝 Notes

- Database migrations auto-run on startup (Development)
- Admin user seeded in Development environment
- Email service logs failures but continues operation
- Token cookies use `SameSite=Lax` for CSRF protection
- Set `Secure=true` in production (requires HTTPS)

## 🔗 Frontend Integration

Frontend should:
1. Send credentials to `/auth/login`
2. Cookies stored automatically by browser
3. Make requests with `withCredentials: true` in Axios/Fetch
4. On 401: Call `/auth/refresh` to renew token
5. On logout: POST to `/auth/logout`

