# LucoBot Mobile App - Deployment Guide for Supervisor

## Quick Overview

This is the **LucoBot Mobile Admin App** - a React Native mobile application for faculty members to manage appointment requests from visitors.

**Database Status**: ✅ Already deployed on NeonDB (PostgreSQL)  
**Backend Status**: ❌ Needs deployment (Node.js + Express + Socket.io)  
**Your Task**: Deploy the Node.js backend on Render or Vercel

---

## What You Need to Deploy

### Single Backend to Deploy:
📁 **`lucobot-backend/`** - Node.js backend (Express + Socket.io + PostgreSQL)

---

## Pre-Deployment Checklist

- [ ] NeonDB database is accessible (connection string available)
- [ ] GitHub repository is up to date
- [ ] Render or Vercel account is ready
- [ ] You have the NeonDB connection string handy

---

## Step-by-Step Deployment (Render - Recommended)

### Step 1: Prepare Environment Variables

You'll need the following environment variable from NeonDB:

```
DATABASE_URL=postgresql://user:pass@host/db?sslmode=require
```

**How to get this:**
1. Go to [NeonDB Console](https://console.neon.tech/)
2. Select your project
3. Click "Connection Details"
4. Copy the "Connection string"

### Step 2: Deploy to Render

1. **Go to Render Dashboard**: https://dashboard.render.com/

2. **Create New Web Service**:
   - Click "New +" → "Web Service"
   - Connect your GitHub repository
   - Select the repository containing this code

3. **Configure Service**:
   ```
   Name: lucobot-backend
   Root Directory: lucobot-backend
   Environment: Node
   Build Command: npm install
   Start Command: npm start
   ```

4. **Add Environment Variable**:
   - Go to "Environment" tab
   - Click "Add Environment Variable"
   - Key: `DATABASE_URL`
   - Value: [Your NeonDB connection string]

5. **Deploy**:
   - Click "Create Web Service"
   - Wait for deployment (5-10 minutes)
   - Copy the service URL (e.g., `https://lucobot-backend.onrender.com`)

### Step 3: Verify Deployment

Test the backend health endpoint:

```bash
curl https://your-backend-url.onrender.com/api/health
```

Expected response:
```json
{
  "success": true,
  "message": "LucoBot Admin API is running",
  "features": {
    "fuzzy_search": true,
    "socket_io": true,
    "image_support": true,
    "delete_appointments": true
  }
}
```

### Step 4: Update Mobile App Configuration

Send the deployed backend URL to the mobile app developers so they can update:

**File to update**: Root `.env` file
```env
EXPO_PUBLIC_SERVER_URL=https://your-backend-url.onrender.com
```

---

## Alternative: Deploy to Vercel

### Step 1: Install Vercel CLI

```bash
npm install -g vercel
```

### Step 2: Create vercel.json

In the `lucobot-backend/` folder, create `vercel.json`:

```json
{
  "version": 2,
  "builds": [
    {
      "src": "server.js",
      "use": "@vercel/node"
    }
  ],
  "routes": [
    {
      "src": "/(.*)",
      "dest": "server.js"
    }
  ],
  "env": {
    "NODE_ENV": "production"
  }
}
```

### Step 3: Deploy

```bash
cd lucobot-backend
vercel --prod
```

### Step 4: Add Environment Variables

1. Go to [Vercel Dashboard](https://vercel.com/dashboard)
2. Select your project
3. Go to Settings → Environment Variables
4. Add `DATABASE_URL` with your NeonDB connection string
5. Redeploy if needed

---

## Backend File Structure

```
lucobot-backend/
├── server.js                 # Main Express server (entry point)
├── package.json              # Dependencies
├── database_postgres.sql     # Database schema (already applied)
├── .env.example              # Environment variables template
└── README.md                 # Documentation
```

---

## Backend Features

### What the backend does:
- ✅ User authentication (faculty login)
- ✅ Appointment management (create, approve, reject, delete)
- ✅ Fuzzy search for employees (name matching with typo tolerance)
- ✅ Real-time updates via Socket.io (push notifications to mobile app)
- ✅ Image support (Base64 visitor photos from tablet)
- ✅ PostgreSQL database integration (NeonDB)

### API Endpoints:
- `POST /api/login` - Faculty login
- `GET /api/appointments/:employeeId` - Get appointments
- `POST /api/appointments/create` - Create appointment (from tablet)
- `PUT /api/appointments/:id/approve` - Approve appointment
- `PUT /api/appointments/:id/reject` - Reject appointment
- `DELETE /api/appointments/:id` - Delete appointment
- `POST /api/search_employee` - Search faculty by name
- `GET /api/health` - Health check

---

## Important Notes

### Environment Variables

Only **ONE** environment variable is required:

```env
DATABASE_URL=postgresql://user:pass@host/db?sslmode=require
```

The `PORT` variable is automatically set by Render/Vercel.

### Database Connection

- The backend connects to **NeonDB PostgreSQL** (already deployed)
- SSL is automatically enabled for remote databases
- No additional database setup needed

### Socket.io Configuration

- Socket.io is configured for both WebSocket and polling transports
- CORS is set to allow all origins (`*`) - consider restricting in production
- Real-time updates are sent to faculty mobile app when new appointments arrive

---

## Troubleshooting

### Build Fails

**Issue**: `npm install` fails  
**Solution**: Check if `package.json` is correct and all dependencies are valid

### Database Connection Error

**Issue**: "Missing DATABASE_URL" or connection failed  
**Solution**: 
- Verify `DATABASE_URL` is set in environment variables
- Check if NeonDB connection string is correct
- Ensure SSL mode is included: `?sslmode=require`

### Health Check Fails

**Issue**: `/api/health` returns 404 or 500  
**Solution**:
- Check server logs in Render/Vercel dashboard
- Verify `server.js` is the entry point
- Ensure PORT is not hardcoded (use `process.env.PORT || 3000`)

### Socket.io Not Working

**Issue**: Mobile app not receiving real-time updates  
**Solution**:
- Ensure backend supports WebSocket (Render and Vercel do)
- Check if CORS is properly configured
- Verify mobile app is connecting to the correct URL

---

## Testing the Deployment

### 1. Test Health Endpoint

```bash
curl https://your-backend-url.com/api/health
```

### 2. Test Login

```bash
curl -X POST https://your-backend-url.com/api/login \
  -H "Content-Type: application/json" \
  -d '{"username": "EMP001", "password": "admin123"}'
```

### 3. Test Employee Search

```bash
curl -X POST https://your-backend-url.com/api/search_employee \
  -H "Content-Type: application/json" \
  -d '{"name": "John Smith"}'
```

---

## Security Considerations (Production)

Before going live, consider:

1. **Password Security**: Update the demo password logic in `server.js` (line 304)
2. **CORS**: Restrict allowed origins instead of `*`
3. **Rate Limiting**: Add rate limiting middleware
4. **Environment Variables**: Never commit `.env` to version control
5. **HTTPS Only**: Ensure all communication uses HTTPS

---

## Contact Information

If you encounter issues:
- Check Render/Vercel logs first
- Review NeonDB dashboard for database status
- Refer to the main `README.md` for detailed documentation

---

## Summary

**What you need to deploy**: `lucobot-backend/` folder  
**Where to deploy**: Render (recommended) or Vercel  
**Environment variable needed**: `DATABASE_URL` (from NeonDB)  
**Deployment time**: ~10-15 minutes  
**Expected URL format**: `https://lucobot-backend.onrender.com`

After deployment, share the backend URL so it can be configured in the mobile app.

---

✅ **Database**: Already deployed on NeonDB  
⏳ **Backend**: Ready to deploy (follow this guide)  
📱 **Mobile App**: Will be configured after backend deployment
