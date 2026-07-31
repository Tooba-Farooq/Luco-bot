const express = require('express');
const { Pool } = require('pg');
const bcrypt = require('bcrypt');
const cors = require('cors');
const dotenv = require('dotenv');
const http = require('http');
const { Server } = require('socket.io');

dotenv.config();

const app = express();
const server = http.createServer(app);
const io = new Server(server, {
    cors: {
        origin: "*",
        methods: ["GET", "POST", "PUT", "DELETE"]
    }
});

const PORT = process.env.PORT || 3000;

// Middleware
app.use(cors());
app.use(express.json({ limit: '50mb' })); // Increased limit for Base64 images
app.use(express.urlencoded({ limit: '50mb', extended: true }));

// ============================================================
// DATABASE (Postgres / Neon)
// ============================================================
const DATABASE_URL = process.env.DATABASE_URL;
if (!DATABASE_URL) {
    console.error('Missing DATABASE_URL. Set it to your Neon Postgres connection string.');
    process.exit(1);
}

// Neon requires SSL in most setups. Enable SSL automatically for non-local URLs.
const shouldUseSsl = !/localhost|127\.0\.0\.1/i.test(DATABASE_URL);

// Create Postgres connection pool
const pool = new Pool({
    connectionString: DATABASE_URL,
    ...(shouldUseSsl ? { ssl: { rejectUnauthorized: false } } : {})
});

// Convert mysql-style '?' placeholders to pg-style '$1, $2, ...'
function toPgPlaceholders(sql) {
    let i = 0;
    return sql.replace(/\?/g, () => `$${++i}`);
}

// Test database connection
async function testConnection() {
    try {
        const client = await pool.connect();
        console.log('Database connected successfully');
        client.release();
    } catch (error) {
        console.error('Database connection failed:', error);
    }
}

testConnection();

// ============================================================
// FUZZY SEARCH UTILITIES
// ============================================================

/**
 * Calculate Levenshtein distance between two strings
 * @param {string} str1 - First string
 * @param {string} str2 - Second string
 * @returns {number} - Edit distance
 */
function levenshteinDistance(str1, str2) {
    const m = str1.length;
    const n = str2.length;
    
    // Create 2D array for dynamic programming
    const dp = Array(m + 1).fill(null).map(() => Array(n + 1).fill(0));
    
    // Initialize first column
    for (let i = 0; i <= m; i++) {
        dp[i][0] = i;
    }
    
    // Initialize first row
    for (let j = 0; j <= n; j++) {
        dp[0][j] = j;
    }
    
    // Fill the matrix
    for (let i = 1; i <= m; i++) {
        for (let j = 1; j <= n; j++) {
            if (str1[i - 1] === str2[j - 1]) {
                dp[i][j] = dp[i - 1][j - 1];
            } else {
                dp[i][j] = 1 + Math.min(
                    dp[i - 1][j],     // deletion
                    dp[i][j - 1],     // insertion
                    dp[i - 1][j - 1]  // substitution
                );
            }
        }
    }
    
    return dp[m][n];
}

/**
 * Calculate similarity score (0-100) between two strings
 * @param {string} str1 - First string
 * @param {string} str2 - Second string
 * @returns {number} - Similarity percentage (0-100)
 */
function calculateSimilarity(str1, str2) {
    if (!str1 || !str2) return 0;
    
    const maxLength = Math.max(str1.length, str2.length);
    if (maxLength === 0) return 100;
    
    const distance = levenshteinDistance(str1, str2);
    return Math.round((1 - distance / maxLength) * 100);
}

/**
 * Normalize name for comparison:
 * - Remove titles (Mr., Mrs., Dr., Prof., etc.)
 * - Remove extra whitespace
 * - Convert to lowercase
 * @param {string} name - Input name
 * @returns {string} - Normalized name
 */
function normalizeName(name) {
    if (!name) return '';
    
    // Titles to remove (case insensitive)
    const titles = [
        'mr\\.?', 'mrs\\.?', 'ms\\.?', 'miss', 
        'dr\\.?', 'prof\\.?', 'professor',
        'sir', 'madam', 'ma\'am',
        'engr\\.?', 'engineer', 'atty\\.?', 'attorney'
    ];
    
    let normalized = name.toLowerCase().trim();
    
    // Remove titles
    const titleRegex = new RegExp(`^(${titles.join('|')})\\s+`, 'i');
    normalized = normalized.replace(titleRegex, '');
    
    // Remove extra whitespace
    normalized = normalized.replace(/\s+/g, ' ').trim();
    
    return normalized;
}

/**
 * Fuzzy search for employees by name
 * FIX 3: Improved partial name matching - if search term matches ANY part of name exactly or closely, include it
 * @param {string} searchName - Name to search for
 * @param {number} threshold - Minimum similarity score (default: 60, lowered for better partial matching)
 * @returns {Promise<Array>} - Array of matching employees with scores
 */
async function fuzzySearchEmployees(searchName, threshold = 60) {
    try {
        // Get all active employees
        const employeesResult = await pool.query(
            'SELECT employee_id, name, department, designation FROM employees WHERE is_active = TRUE'
        );
        const employees = employeesResult.rows;
        
        const normalizedSearch = normalizeName(searchName);
        const matches = [];
        
        for (const emp of employees) {
            const normalizedEmpName = normalizeName(emp.name);
            const similarity = calculateSimilarity(normalizedSearch, normalizedEmpName);
            
            // Also check partial matches (first name, last name)
            const searchParts = normalizedSearch.split(' ').filter(p => p.length > 0);
            const nameParts = normalizedEmpName.split(' ').filter(p => p.length > 0);
            
            let bestPartialScore = similarity;
            let hasExactPartMatch = false;
            
            // FIX 3: Check if search term matches any part of the name
            // Give high priority to exact partial matches (e.g., "John" in "Dr. John Smith")
            for (const searchPart of searchParts) {
                for (const namePart of nameParts) {
                    // Check for exact match on any name part
                    if (searchPart === namePart) {
                        hasExactPartMatch = true;
                        bestPartialScore = 100;
                    } else {
                        const partialScore = calculateSimilarity(searchPart, namePart);
                        if (partialScore > bestPartialScore) {
                            bestPartialScore = partialScore;
                        }
                    }
                    
                    // Also check if search contains name part or vice versa
                    if (namePart.includes(searchPart) || searchPart.includes(namePart)) {
                        const containScore = Math.max(85, bestPartialScore);
                        if (containScore > bestPartialScore) {
                            bestPartialScore = containScore;
                        }
                    }
                }
            }
            
            // FIX 3: Weight partial matches more heavily when search is a single word
            let finalScore;
            if (searchParts.length === 1) {
                // Single word search - prioritize partial matching
                finalScore = Math.round((similarity * 0.3) + (bestPartialScore * 0.7));
            } else {
                // Multi-word search - balanced weighting
                finalScore = Math.round((similarity * 0.5) + (bestPartialScore * 0.5));
            }
            
            // Boost score for exact partial matches
            if (hasExactPartMatch) {
                finalScore = Math.max(finalScore, 90);
            }
            
            if (finalScore >= threshold) {
                matches.push({
                    employee_id: emp.employee_id,
                    name: emp.name,
                    department: emp.department,
                    designation: emp.designation,
                    similarity: finalScore
                });
            }
        }
        
        // Sort by similarity score (highest first)
        matches.sort((a, b) => b.similarity - a.similarity);
        
        return matches;
    } catch (error) {
        console.error('Fuzzy search error:', error);
        return [];
    }
}

// ============================================================
// SOCKET.IO CONNECTION HANDLING
// ============================================================

io.on('connection', (socket) => {
    console.log(`Client connected: ${socket.id}`);
    
    // Handle employee joining their room (for targeted updates)
    socket.on('join_employee_room', (employeeId) => {
        socket.join(`employee_${employeeId}`);
        console.log(`Socket ${socket.id} joined room: employee_${employeeId}`);
    });
    
    socket.on('disconnect', () => {
        console.log(`Client disconnected: ${socket.id}`);
    });
});

// Function to emit new appointment to specific employee
function emitNewAppointment(employeeId, appointment) {
    io.to(`employee_${employeeId}`).emit('new_appointment', appointment);
    console.log(`Emitted new_appointment to employee_${employeeId}`);
}

// ============================================================
// API ENDPOINTS
// ============================================================

// Login endpoint
app.post('/api/login', async (req, res) => {
    try {
        const { username, password } = req.body;

        if (!username || !password) {
            return res.status(400).json({
                success: false,
                message: 'Username and password are required'
            });
        }

        // Query employee by email or employee_id
        const rowsResult = await pool.query(
            toPgPlaceholders('SELECT * FROM employees WHERE (email = ? OR employee_id = ?) AND is_active = TRUE'),
            [username, username]
        );
        const rows = rowsResult.rows;

        if (rows.length === 0) {
            return res.status(401).json({
                success: false,
                message: 'Invalid credentials'
            });
        }

        const employee = rows[0];

        // For demo purposes, we'll use simple password comparison
        // In production, use bcrypt.compare(password, employee.password_hash)
        const isValidPassword = password === 'admin123'; // Demo password

        if (!isValidPassword) {
            return res.status(401).json({
                success: false,
                message: 'Invalid credentials'
            });
        }

        // Create a clean employee object without password
        const employeeData = {
            employee_id: employee.employee_id,
            name: employee.name,
            email: employee.email,
            department: employee.department,
            designation: employee.designation,
            phone: employee.phone,
            created_at: employee.created_at,
            updated_at: employee.updated_at,
            is_active: employee.is_active
        };

        res.json({
            success: true,
            message: 'Login successful',
            employee: employeeData
        });

    } catch (error) {
        console.error('Login error:', error);
        res.status(500).json({
            success: false,
            message: 'Internal server error'
        });
    }
});

// Fuzzy search endpoint for employee lookup (from tablet)
app.post('/api/search_employee', async (req, res) => {
    try {
        const { name, threshold = 80 } = req.body;
        
        if (!name || name.trim().length === 0) {
            return res.status(400).json({
                success: false,
                message: 'Name is required',
                matches: [],
                match_type: 'none'
            });
        }
        
        const matches = await fuzzySearchEmployees(name.trim(), threshold);
        
        let matchType;
        if (matches.length === 0) {
            matchType = 'none';
        } else if (matches.length === 1) {
            matchType = 'single';
        } else {
            matchType = 'multiple';
        }
        
        res.json({
            success: true,
            matches: matches,
            match_type: matchType,
            message: matchType === 'none' 
                ? 'No matching employee found' 
                : matchType === 'single'
                    ? `Found: ${matches[0].name}`
                    : `Found ${matches.length} possible matches`
        });
        
    } catch (error) {
        console.error('Search employee error:', error);
        res.status(500).json({
            success: false,
            message: 'Search failed',
            matches: [],
            match_type: 'none'
        });
    }
});

// Create appointment from tablet (with image support)
app.post('/api/appointments/create', async (req, res) => {
    try {
        const { 
            employee_id, 
            visitor_name, 
            purpose, 
            visitor_image_base64,
            photo_consent 
        } = req.body;
        
        if (!employee_id || !visitor_name || !purpose) {
            return res.status(400).json({
                success: false,
                message: 'employee_id, visitor_name, and purpose are required'
            });
        }
        
        // Verify employee exists
        const empRowsResult = await pool.query(
            toPgPlaceholders('SELECT employee_id, name FROM employees WHERE employee_id = ? AND is_active = TRUE'),
            [employee_id]
        );
        const empRows = empRowsResult.rows;
        
        if (empRows.length === 0) {
            return res.status(404).json({
                success: false,
                message: 'Employee not found'
            });
        }
        
        // Insert appointment with current date/time
        const currentDate = new Date();
        const dateStr = currentDate.toISOString().split('T')[0];
        const timeStr = currentDate.toTimeString().split(' ')[0];
        
        // Prepare image data (only if consent given)
        const imageData = photo_consent && visitor_image_base64 ? visitor_image_base64 : null;
        
        const insertResult = await pool.query(
            toPgPlaceholders(
                `INSERT INTO appointment_requests 
                 (employee_id, student_name, preferred_date, preferred_time, purpose, visitor_image, status) 
                 VALUES (?, ?, ?, ?, ?, ?, 'pending')
                 RETURNING id`
            ),
            [employee_id, visitor_name, dateStr, timeStr, purpose, imageData]
        );

        const appointmentId = insertResult.rows[0]?.id;
        
        // Fetch the created appointment
        const newAppointmentResult = await pool.query(
            toPgPlaceholders(
                `SELECT ar.*, e.name as faculty_name 
                 FROM appointment_requests ar 
                 JOIN employees e ON ar.employee_id = e.employee_id 
                 WHERE ar.id = ?`
            ),
            [appointmentId]
        );
        const newAppointment = newAppointmentResult.rows;
        
        // Emit socket event to admin
        if (newAppointment.length > 0) {
            emitNewAppointment(employee_id, newAppointment[0]);
        }
        
        res.json({
            success: true,
            message: 'Appointment created successfully',
            appointment_id: appointmentId,
            faculty_name: empRows[0].name
        });
        
    } catch (error) {
        console.error('Create appointment error:', error);
        res.status(500).json({
            success: false,
            message: 'Failed to create appointment'
        });
    }
});

// Get appointments for specific employee
app.get('/api/appointments/:employeeId', async (req, res) => {
    try {
        const { employeeId } = req.params;

        const rowsResult = await pool.query(
            toPgPlaceholders(
                `SELECT ar.id, ar.employee_id, ar.student_name, ar.preferred_date, 
                        ar.preferred_time, ar.purpose, ar.status, ar.visitor_image,
                        ar.created_at, ar.updated_at, e.name as faculty_name 
                 FROM appointment_requests ar 
                 JOIN employees e ON ar.employee_id = e.employee_id 
                 WHERE ar.employee_id = ? 
                 ORDER BY ar.created_at DESC`
            ),
            [employeeId]
        );
        const rows = rowsResult.rows;

        res.json({
            success: true,
            appointments: rows
        });

    } catch (error) {
        console.error('Fetch appointments error:', error);
        res.status(500).json({
            success: false,
            message: 'Failed to fetch appointments'
        });
    }
});

// Approve appointment
app.put('/api/appointments/:appointmentId/approve', async (req, res) => {
    try {
        const { appointmentId } = req.params;
        const { employee_id } = req.body;

        // Verify the appointment belongs to the employee
        const checkRowsResult = await pool.query(
            toPgPlaceholders('SELECT * FROM appointment_requests WHERE id = ? AND employee_id = ?'),
            [appointmentId, employee_id]
        );
        const checkRows = checkRowsResult.rows;

        if (checkRows.length === 0) {
            return res.status(404).json({
                success: false,
                message: 'Appointment not found or unauthorized'
            });
        }

        // Update appointment status
        await pool.query(
            toPgPlaceholders('UPDATE appointment_requests SET status = ? WHERE id = ?'),
            ['approved', appointmentId]
        );

        res.json({
            success: true,
            message: 'Appointment approved successfully'
        });

    } catch (error) {
        console.error('Approve appointment error:', error);
        res.status(500).json({
            success: false,
            message: 'Failed to approve appointment'
        });
    }
});

// Reject appointment
app.put('/api/appointments/:appointmentId/reject', async (req, res) => {
    try {
        const { appointmentId } = req.params;
        const { employee_id } = req.body;

        // Verify the appointment belongs to the employee
        const checkRowsResult = await pool.query(
            toPgPlaceholders('SELECT * FROM appointment_requests WHERE id = ? AND employee_id = ?'),
            [appointmentId, employee_id]
        );
        const checkRows = checkRowsResult.rows;

        if (checkRows.length === 0) {
            return res.status(404).json({
                success: false,
                message: 'Appointment not found or unauthorized'
            });
        }

        // Update appointment status
        await pool.query(
            toPgPlaceholders('UPDATE appointment_requests SET status = ? WHERE id = ?'),
            ['rejected', appointmentId]
        );

        res.json({
            success: true,
            message: 'Appointment rejected successfully'
        });

    } catch (error) {
        console.error('Reject appointment error:', error);
        res.status(500).json({
            success: false,
            message: 'Failed to reject appointment'
        });
    }
});

// Delete appointment (for clearing approved appointments)
app.delete('/api/appointments/:appointmentId', async (req, res) => {
    try {
        const { appointmentId } = req.params;
        const { employee_id } = req.body;

        // Verify the appointment belongs to the employee
        const checkRowsResult = await pool.query(
            toPgPlaceholders('SELECT * FROM appointment_requests WHERE id = ? AND employee_id = ?'),
            [appointmentId, employee_id]
        );
        const checkRows = checkRowsResult.rows;

        if (checkRows.length === 0) {
            return res.status(404).json({
                success: false,
                message: 'Appointment not found or unauthorized'
            });
        }

        // Delete the appointment
        await pool.query(
            toPgPlaceholders('DELETE FROM appointment_requests WHERE id = ?'),
            [appointmentId]
        );

        res.json({
            success: true,
            message: 'Appointment deleted successfully'
        });

    } catch (error) {
        console.error('Delete appointment error:', error);
        res.status(500).json({
            success: false,
            message: 'Failed to delete appointment'
        });
    }
});

// Health check endpoint
app.get('/api/health', (req, res) => {
    res.json({
        success: true,
        message: 'LucoBot Admin API is running',
        timestamp: new Date().toISOString(),
        features: {
            fuzzy_search: true,
            socket_io: true,
            image_support: true,
            delete_appointments: true
        }
    });
});

// Start server with Socket.IO
server.listen(PORT, () => {
    console.log('='.repeat(50));
    console.log('LucoBot Admin Server');
    console.log('='.repeat(50));
    console.log(`Server running on port ${PORT}`);
    console.log('Features:');
    console.log('  ✓ Fuzzy search with Levenshtein distance');
    console.log('  ✓ Socket.IO for real-time updates');
    console.log('  ✓ Image support with Base64 encoding');
    console.log('='.repeat(50));
});