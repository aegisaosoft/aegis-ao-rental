# Database Migration Guide

## Current Status
Your database needs to be updated to support customer roles.

## What needs to be done:
1. Run the migration script to add role support
2. Update your user to have 'mainadmin' role
3. Restart the backend API
4. Log in again to get a new token with role information

## Steps:

### Option 1: Using psql command line (if you have it installed)
```bash
psql -U postgres -d your_database_name -f COMPLETE_MIGRATION.sql
```

### Option 2: Using pgAdmin or any PostgreSQL GUI
1. Open your PostgreSQL client (pgAdmin, DataGrip, etc.)
2. Connect to your database
3. Open the file `COMPLETE_MIGRATION.sql`
4. Execute the entire script

### Option 3: Using Azure Portal
If you're using Azure PostgreSQL:
1. Go to Azure Portal → Your PostgreSQL Database
2. Open Query Editor or Azure Data Studio
3. Copy the contents of `COMPLETE_MIGRATION.sql`
4. Paste and execute

## After Migration:
1. Restart your backend API
2. Log out from the frontend
3. Log in again (you should get a token with your role)
4. The edit button should now appear for vehicles

## Verify Migration:
Check if your user has the correct role:
```sql
SELECT customer_id, email, first_name, last_name, role, is_active, company_id 
FROM customers 
WHERE email = 'orlovus@gmail.com';
```

Expected result:
- role: 'mainadmin'
- is_active: true
- company_id: (your company ID or null)

## If Migration Fails:
If you get errors like "column already exists", you can skip those ALTER TABLE commands.
The script uses IF NOT EXISTS to handle this gracefully.
