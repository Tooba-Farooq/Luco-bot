-- LucoBot Database Migration Script
-- Run this script to update existing databases to the new schema
-- This adds visitor_image support and removes email/phone fields

-- USAGE (Windows PowerShell):
--   mysql -u root -p lucobot_admin -e "source C:/path/to/migrate.sql"
-- OR run each command individually in MySQL Workbench/CLI

USE lucobot_admin;

-- Step 1: Add visitor_image column if it doesn't exist
-- If you get "Duplicate column" error, the column already exists (safe to ignore)
ALTER TABLE appointment_requests ADD COLUMN visitor_image LONGTEXT AFTER purpose;

-- Step 2: Remove student_email if it exists  
-- If you get "Can't DROP" error, the column is already gone (safe to ignore)
ALTER TABLE appointment_requests DROP COLUMN student_email;

-- Step 3: Remove student_phone if it exists
-- If you get "Can't DROP" error, the column is already gone (safe to ignore)
ALTER TABLE appointment_requests DROP COLUMN student_phone;

-- Verify the changes
SELECT 'Migration complete! Current columns:' as message;
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = 'lucobot_admin' AND TABLE_NAME = 'appointment_requests'
ORDER BY ORDINAL_POSITION;