IMPORTANT: WHICH SCRIPT TO USE
================================

DO NOT USE: add_id_to_vehicle_model.sql
This script is broken and will fail!

USE INSTEAD: recreate_vehicle_model_with_id.sql

This script:
1. Deletes all vehicles data
2. Drops the vehicle_model table completely (line 27: DROP TABLE IF EXISTS vehicle_model CASCADE)
3. Recreates vehicle_model with UUID id primary key
4. Sets up all constraints and indexes correctly

File location: aegis-ao-rental/recreate_vehicle_model_with_id.sql

