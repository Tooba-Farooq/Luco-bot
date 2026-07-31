-- Create database
CREATE DATABASE IF NOT EXISTS lucobot_admin;
USE lucobot_admin;

-- Employee table for authentication and basic info
CREATE TABLE employees (
    employee_id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    department VARCHAR(100),
    designation VARCHAR(100),
    phone VARCHAR(20),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE
);

-- Appointment requests table (updated: removed student_email/phone, added visitor_image)
CREATE TABLE appointment_requests (
    id INT AUTO_INCREMENT PRIMARY KEY,
    employee_id VARCHAR(50) NOT NULL,
    student_name VARCHAR(100) NOT NULL,
    preferred_date DATE NOT NULL,
    preferred_time TIME NOT NULL,
    purpose TEXT NOT NULL,
    visitor_image LONGTEXT,  -- Base64 encoded image (nullable if consent denied)
    status ENUM('pending', 'approved', 'rejected') DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (employee_id) REFERENCES employees(employee_id) ON DELETE CASCADE
);

-- Insert dummy employees
INSERT INTO employees (employee_id, name, email, password_hash, department, designation) VALUES
('EMP001', 'Dr. John Smith', 'john.smith@university.edu', '$2b$10$dummy_hash_1', 'Computer Science', 'Professor'),
('EMP002', 'Dr. Sarah Johnson', 'sarah.johnson@university.edu', '$2b$10$dummy_hash_2', 'Mathematics', 'Associate Professor'),
('EMP003', 'Dr. Michael Brown', 'michael.brown@university.edu', '$2b$10$dummy_hash_3', 'Physics', 'Assistant Professor'),
('EMP005', 'Dr. Mubashir', 'mubashir@university.edu', '$2b$10$dummy_hash_4', 'Computer Science', 'Professor');

-- Insert dummy appointment requests (without email/phone)
INSERT INTO appointment_requests (employee_id, student_name, preferred_date, preferred_time, purpose, status) VALUES
('EMP001', 'Alice Wilson', '2024-01-15', '10:00:00', 'Discuss thesis proposal', 'pending'),
('EMP001', 'Bob Davis', '2024-01-16', '14:00:00', 'Course guidance', 'pending'),
('EMP002', 'Carol Martinez', '2024-01-17', '11:00:00', 'Research collaboration', 'approved'),
('EMP003', 'David Lee', '2024-01-18', '15:30:00', 'Lab access request', 'pending');

-- Migration script for existing databases:
-- ALTER TABLE appointment_requests ADD COLUMN visitor_image LONGTEXT AFTER purpose;
-- ALTER TABLE appointment_requests DROP COLUMN student_email;
-- ALTER TABLE appointment_requests DROP COLUMN student_phone;