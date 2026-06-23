# Deployment: Render + Neon + Cloudinary

## Prerequisites

- [Neon](https://neon.tech) PostgreSQL database (pooled connection string)
- [Cloudinary](https://cloudinary.com) account (Cloud name, API Key, API Secret)
- [Render](https://render.com) account
- Copy `.env.example` to `.env` for local development

## Local development

1. Copy `.env.example` to `.env` and fill in values.
2. Download OCR data (if not present):

   ```bash
   curl -o tessdata/eng.traineddata \
     https://github.com/tesseract-ocr/tessdata/raw/main/eng.traineddata
   ```

3. Run:

   ```bash
   dotnet run
   ```

For local file storage instead of Cloudinary, set `Storage__Provider=Local` in `.env`.

## Neon connection string

Convert your Neon URL to Npgsql format for `ConnectionStrings__DefaultConnection`:

```text
Host=YOUR_HOST-pooler.region.aws.neon.tech;Database=neondb;Username=YOUR_USER;Password=YOUR_PASSWORD;Ssl Mode=Require
```

Use the **pooled** host from the Neon dashboard.

## Render deployment (Docker)

1. Push this repo to GitHub.
2. In Render: **New** → **Blueprint** (uses `render.yaml`) or **Web Service** → **Docker**.
3. Set **Root Directory** to `PharmacyFinder.API` if the repo root is the parent `Pharmacy` folder.
4. Add environment variables (same keys as `.env.example`):

   | Variable | Notes |
   |----------|--------|
   | `ConnectionStrings__DefaultConnection` | Neon pooled string |
   | `Jwt__Secret` | Strong random secret |
   | `Cors__AllowedOrigins` | Production frontend URL(s), comma-separated — e.g. `https://your-app.onrender.com` |
   | `Cloudinary__CloudName` | From Cloudinary dashboard |
   | `Cloudinary__ApiKey` | From Cloudinary dashboard |
   | `Cloudinary__ApiSecret` | From Cloudinary dashboard |
   | `Storage__Provider` | `Cloudinary` |
   | `BootstrapAdmin__Email` | First deploy only |
   | `BootstrapAdmin__Password` | First deploy only |

5. Deploy. Migrations run automatically on startup.
6. Log in as bootstrap admin, then **remove** `BootstrapAdmin__*` env vars from Render.

## Admin: promote users

```http
PUT /api/admin/users/{userId}/role
Authorization: Bearer {admin-jwt}
Content-Type: application/json

{ "role": "Admin" }
```

## Health check

```http
GET /health
```

Returns `{ "status": "healthy" }` — used by Render.

## Docker (manual)

From `PharmacyFinder.API`:

```bash
docker build -t pharmacyfinder-api .
docker run -p 8080:8080 --env-file .env pharmacyfinder-api
```

## Security notes

- Never commit `.env` or real secrets to git.
- Rotate Neon/Cloudinary credentials if exposed.
- Remove bootstrap admin env vars after first login.
