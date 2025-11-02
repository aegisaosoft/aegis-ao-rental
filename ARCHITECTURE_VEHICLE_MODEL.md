# Vehicle-Model Architecture

## Overview
This document describes the relationship between Vehicles, Models, and the VehicleModel junction table.

## Current Architecture (Before Migration)
- `vehicles` table has: `make`, `model`, `year` (redundant data)
- `models` table has: `make`, `model_name`, `year`, `category_id`, etc.
- `vehicle_model` table exists but is NOT used
- Vehicles store duplicate make/model/year data

## Target Architecture (After Migration)
- `vehicles` table: NO `make`, `model`, `year` columns
- `models` table: Contains unique combinations of make/model_name/year
- `vehicle_model` table: Links vehicles to models (junction/relationship table)

### Relationships
```
Company (1) -----> (Many) Vehicles
                         |
                         |
                         v
                    vehicle_model (junction table)
                         |
                         |
                         v
                    Models (Ford Mustang 2025)
```

### Example Data
```
Models Table:
- id: guid-1
- make: Ford
- model_name: Mustang
- year: 2025
- category_id: sports-car-guid

Vehicles Table:
- id: vehicle-1
- company_id: company-1
- color: Red
- license_plate: ABC-123
- vin: xxxxxxxxxxxxxx
- daily_rate: 70.00

- id: vehicle-2
- company_id: company-1
- color: Blue
- license_plate: XYZ-789
- vin: yyyyyyyyyyyyyyy
- daily_rate: 70.00

vehicle_model Table:
- vehicle_id: vehicle-1  -> model_id: guid-1
- vehicle_id: vehicle-2  -> model_id: guid-1

Multiple vehicles share the SAME model!
```

## Key Points
1. **Template Pattern**: `vehicle_model` acts as a template/catalog
2. **Rate Management**: Daily rates can be set at the model level OR vehicle level
3. **No Redundancy**: Make/model/year stored only in `models` table
4. **Company Scoping**: Vehicles belong to companies, models are shared across companies
5. **Flexibility**: Companies can have multiple vehicles of the same model

## Migration Steps
1. ✅ Create `vehicle_model` table
2. ✅ Populate `vehicle_model` from existing vehicle data
3. ⏳ Remove `make`, `model`, `year` from `vehicles` table
4. ⏳ Update C# Vehicle entity to remove make/model/year properties
5. ⏳ Update all queries to JOIN through `vehicle_model` to get model data
6. ⏳ Update frontend to handle new data structure

## Impact Areas
- Vehicle creation/update endpoints
- Vehicle listing/search endpoints
- Admin dashboard fleet management
- Vehicle filtering by make/model/year
- All DTOs that include Make/Model/Year
- Frontend components displaying vehicle info

