# LucoBot Mobile App

A React Native mobile application built with Expo for the LucoBot robot system. This app serves as an **Admin Portal for Faculty Members** to manage appointment requests from visitors.

## Project Overview

### Functionalities
- 🔐 **User Authentication**: Faculty login using Employee ID or email
- 📋 **Appointment Management**: View all pending, approved, and declined appointment requests
- ✅ **Real-time Approvals**: Accept or decline visitor appointments with instant updates
- 🔔 **Live Notifications**: Receive real-time notifications when new appointment requests arrive
- 🖼️ **Visitor Photos**: View visitor photos (if consent was given) when reviewing appointments
- 🗑️ **History Management**: Remove completed appointments from the list
- 🔄 **Socket.io Integration**: Real-time bidirectional communication with the backend

### Tech Stack
- **Frontend**: React Native with Expo SDK 54
- **UI Components**: Expo Vector Icons, React Native Reanimated
- **Real-time Communication**: Socket.io Client
- **Backend**: Node.js with Express
- **Database**: PostgreSQL (hosted on NeonDB)
- **API**: RESTful endpoints + WebSocket

---

## Prerequisites

- Node.js (v16 or higher)
- npm or yarn
- Expo CLI (optional, can use npx)
- Android/iOS device or emulator

---

## Local Development Setup

### 1. Install Dependencies

```bash
npm install
```

### 2. Backend Setup

The mobile app requires a Node.js backend to function. See `lucobot-backend/` folder for backend setup instructions.

**For local development:**

```bash
cd lucobot-backend
npm install
```

Create `.env` file in `lucobot-backend/`:
```env
PORT=3000
DATABASE_URL=postgresql://username:password@host/database?sslmode=require
```

Start the backend:
```bash
npm start
```

The backend will run on `http://localhost:3000`

### 3. Mobile App Configuration

Create a `.env` file in the root directory:

```env
# For local development
EXPO_PUBLIC_SERVER_URL=http://localhost:3000

# For production (after backend deployment)
# EXPO_PUBLIC_SERVER_URL=https://your-backend-url.onrender.com
```

See `env.example` for template and detailed instructions.

---

## Running the Application

### Start Mobile App

```bash
npm start
```

Or use platform-specific commands:

```bash
npm run android    # Android emulator/device
npm run ios        # iOS simulator/device (macOS only)
npm run web        # Web browser
```

Scan the QR code with **Expo Go** app on your mobile device, or press the corresponding key to open in emulator/simulator.

---

## Project Structure

```
├── app/                          # Application screens
│   ├── home-screen/             # Appointment management UI
│   ├── login/                   # Faculty login screen
│   └── Splash-screen/           # App splash screen
├── components/                   # Reusable UI components
├── constants/                    # App constants and theme
├── lucobot-backend/             # Node.js backend
│   ├── server.js                # Express + Socket.io server
│   ├── package.json             # Backend dependencies
│   ├── database_postgres.sql    # PostgreSQL schema
│   └── env.example              # Backend environment template
├── package.json                 # Mobile app dependencies
├── env.example                  # Mobile app environment template
├── README.md                    # This file
└── DEPLOYMENT_GUIDE.md          # Production deployment instructions
```

---

## Backend API Endpoints

The backend provides the following endpoints:

### Authentication
- `POST /api/login` - Faculty login

### Appointments
- `GET /api/appointments/:employeeId` - Get all appointments for a faculty member
- `POST /api/appointments/create` - Create new appointment (from tablet)
- `PUT /api/appointments/:appointmentId/approve` - Approve an appointment
- `PUT /api/appointments/:appointmentId/reject` - Reject an appointment
- `DELETE /api/appointments/:appointmentId` - Delete an appointment

### Search
- `POST /api/search_employee` - Fuzzy search for employees (from tablet)

### Health Check
- `GET /api/health` - Server status and features

---

## Database Schema

The database includes:

### Tables
- **`employees`** - Faculty members with authentication credentials
  - employee_id (Primary Key)
  - name, email, password_hash
  - department, designation, phone
  - is_active, created_at, updated_at

- **`appointment_requests`** - Visitor appointment requests
  - id (Primary Key)
  - employee_id (Foreign Key)
  - student_name, preferred_date, preferred_time
  - purpose, visitor_image
  - status (pending, approved, rejected)
  - created_at, updated_at

See `lucobot-backend/database_postgres.sql` for complete schema.

---

## Features in Detail

### Faculty Authentication
- Login with Employee ID or email
- Session management
- Demo password: `admin123` (for development)

### Appointment Management
- Real-time updates via Socket.io
- Sort by status (pending first, then approved)
- Filtered view (rejected appointments automatically removed)
- Visual status indicators (pending, approved, declined)

### Visitor Photos
- Base64 image support
- Photo consent workflow
- Image display in appointment details modal

### Real-time Notifications
- New appointment toast notifications
- Socket.io room-based targeting
- Automatic reconnection on app resume

---

## Environment Variables

### Mobile App (Root `.env`)

```env
# Backend Server URL
EXPO_PUBLIC_SERVER_URL=http://localhost:3000
```

### Backend (`lucobot-backend/.env`)

```env
# Server Port (auto-set by hosting platforms)
PORT=3000

# Database Connection (from NeonDB)
DATABASE_URL=postgresql://username:password@host/database?sslmode=require
```

See `env.example` files for detailed instructions and examples.

---

## Development Tips

### Testing Backend Locally

```bash
# Test health endpoint
curl http://localhost:3000/api/health

# Test login
curl -X POST http://localhost:3000/api/login \
  -H "Content-Type: application/json" \
  -d '{"username": "EMP001", "password": "admin123"}'
```

### Default Credentials

For testing, use:
- **Employee ID**: `EMP001` (or any from database)
- **Email**: `john.smith@university.edu`
- **Password**: `admin123`

See `lucobot-backend/database_postgres.sql` for all seeded employees.

---

## Troubleshooting

### Backend Connection Issues
- Verify backend is running on `http://localhost:3000`
- Check `EXPO_PUBLIC_SERVER_URL` in `.env` file
- Ensure no firewall blocking connections
- Check backend logs for errors

### Socket.io Not Connecting
- Ensure backend is running and Socket.io is initialized
- Check browser console for connection errors
- Verify CORS is enabled in backend (already configured)

### Database Connection Failed
- Verify `DATABASE_URL` in backend `.env`
- Check NeonDB dashboard for database status
- Ensure SSL mode is included: `?sslmode=require`

---

## Production Deployment

For deploying the backend to production (Render, Vercel, etc.), see:

📄 **`DEPLOYMENT_GUIDE.md`** - Comprehensive deployment instructions for your supervisor

---

## Scripts

```bash
npm start          # Start Expo development server
npm run android    # Run on Android
npm run ios        # Run on iOS
npm run web        # Run in web browser
npm run lint       # Run ESLint
```

---

## Support

For deployment instructions, refer to `DEPLOYMENT_GUIDE.md`.

For local development issues, check:
- Expo documentation: https://docs.expo.dev/
- React Native documentation: https://reactnative.dev/
- Socket.io documentation: https://socket.io/docs/

---

## License

This project is part of the LucoBot robot receptionist system.
