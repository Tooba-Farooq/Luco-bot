// Migration script to set up database schema on Neon
const { Pool } = require('pg');
require('dotenv').config();

const DATABASE_URL = process.env.DATABASE_URL;
if (!DATABASE_URL) {
    console.error('Missing DATABASE_URL in .env file');
    process.exit(1);
}

const pool = new Pool({
    connectionString: DATABASE_URL,
    ssl: { rejectUnauthorized: false }
});

async function migrate() {
    const client = await pool.connect();
    try {
        console.log('Connected to Neon database...\n');
        
        // Create employees table
        console.log('Creating employees table...');
        await client.query(`
            CREATE TABLE IF NOT EXISTS employees (
                employee_id VARCHAR(50) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                email VARCHAR(100) UNIQUE NOT NULL,
                password_hash VARCHAR(255) NOT NULL,
                department VARCHAR(100),
                designation VARCHAR(100),
                phone VARCHAR(20),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                is_active BOOLEAN DEFAULT TRUE
            )
        `);
        console.log('✓ employees table created');

        // Create appointment_requests table
        console.log('Creating appointment_requests table...');
        await client.query(`
            CREATE TABLE IF NOT EXISTS appointment_requests (
                id SERIAL PRIMARY KEY,
                employee_id VARCHAR(50) NOT NULL,
                student_name VARCHAR(100) NOT NULL,
                preferred_date DATE NOT NULL,
                preferred_time TIME NOT NULL,
                purpose TEXT NOT NULL,
                visitor_image TEXT,
                status VARCHAR(20) DEFAULT 'pending' CHECK (status IN ('pending', 'approved', 'rejected')),
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (employee_id) REFERENCES employees(employee_id) ON DELETE CASCADE
            )
        `);
        console.log('✓ appointment_requests table created');

        // Insert dummy employees (update if exists)
        console.log('Inserting/updating sample employees...');
        await client.query(`
            INSERT INTO employees (employee_id, name, email, password_hash, department, designation) VALUES
            ('EMP001', 'Dr. John Smith', 'john.smith@university.edu', '$2b$10$dummy_hash_1', 'Computer Science', 'Professor'),
            ('EMP002', 'Dr. Sarah Johnson', 'sarah.johnson@university.edu', '$2b$10$dummy_hash_2', 'Mathematics', 'Associate Professor'),
            ('EMP003', 'Dr. Michael Brown', 'michael.brown@university.edu', '$2b$10$dummy_hash_3', 'Physics', 'Assistant Professor'),
            ('EMP005', 'Dr. Mubashir', 'mubashir@university.edu', '$2b$10$dummy_hash_1', 'Computer Science', 'Professor'),
            ('EMP006', 'Syed Misbah Ur Rehman', 'misbah2002@cloud.neduet.edu.pk', '$2b$10$N9qo8uLOickgx2ZMRZoMye.IjefOcX5I.Zp9U1wQkOgq6xJzsHG5S', 'CIS', 'Research Associate'),
            ('EMP007', 'Dr Hashim Raza Khan', 'hahsim@cloud.neduet.edu.pk', '$2b$10$xQZ3Kp9vL2nM4RtY8WqHjO.LmNkPqRsT6UvWxYz9AbCdEfGhIjKl', 'Electronics', 'CoPi'),
            ('EMP008', 'Taha Amjad', 'tahaamjad@neduet.edu.pk', '$2b$10$Hg7Fn3Jk2Lm4Op5Qr6St8u.VwXyZaBcDeFgHiJkLmNoPqRsTuVwX', 'CIS', 'Team Lead')
            ON CONFLICT (employee_id) DO UPDATE SET
                name = EXCLUDED.name,
                email = EXCLUDED.email,
                password_hash = EXCLUDED.password_hash,
                department = EXCLUDED.department,
                designation = EXCLUDED.designation
        `);
        console.log('✓ Sample employees inserted/updated');

        // Insert sample appointments
        console.log('Inserting sample appointments...');
        await client.query(`
            INSERT INTO appointment_requests (employee_id, student_name, preferred_date, preferred_time, purpose, status) VALUES
            ('EMP001', 'Alice Wilson', '2024-01-15', '10:00:00', 'Discuss thesis proposal', 'pending'),
            ('EMP001', 'Bob Davis', '2024-01-16', '14:00:00', 'Course guidance', 'pending'),
            ('EMP002', 'Carol Martinez', '2024-01-17', '11:00:00', 'Research collaboration', 'approved'),
            ('EMP003', 'David Lee', '2024-01-18', '15:30:00', 'Lab access request', 'pending')
            ON CONFLICT DO NOTHING
        `);
        console.log('✓ Sample appointments inserted');

        // Verify
        console.log('\n--- Verification ---');
        const tables = await client.query(`
            SELECT table_name FROM information_schema.tables 
            WHERE table_schema = 'public'
        `);
        console.log('Tables:', tables.rows.map(r => r.table_name).join(', '));
        
        const empCount = await client.query('SELECT COUNT(*) as count FROM employees');
        console.log('Employee count:', empCount.rows[0].count);
        
        const empList = await client.query('SELECT employee_id, name FROM employees');
        console.log('Employees:');
        empList.rows.forEach(e => console.log(`  - ${e.employee_id}: ${e.name}`));
        
        console.log('\n✅ Migration complete!');
        
    } catch (err) {
        console.error('Migration error:', err.message);
        throw err;
    } finally {
        client.release();
        await pool.end();
    }
}

migrate().catch(err => {
    console.error('Migration failed:', err.message);
    process.exit(1);
});
