# Company-Specific Vehicle Rates - Final Architecture

## Problem Solved

**Issue:** Bulk rate updates were affecting ALL companies globally because `vehicle_model` is a shared catalog.

**Solution:** Store rates on individual `vehicles` table for company-specific rates.

## Architecture

### Three-Tier Rate System

1. **`vehicle_model.daily_rate`** - Global catalog template (shared by all companies)
2. **`vehicles.daily_rate`** - Company-specific rate (overrides catalog)
3. **Fallback:** `vehicles.daily_rate ?? vehicle_model.daily_rate ?? 0`

### Rate Display Priority

```
Displayed Rate = vehicles.daily_rate ?? vehicle_model.daily_rate ?? 0
```

- If vehicle has a rate → use it (company-specific)
- Else if catalog has a rate → use it (global default)
- Else → 0

## Implementation

### 1. Database Schema

```sql
ALTER TABLE vehicles ADD COLUMN daily_rate NUMERIC(10,2);
```

- Added `DailyRate` property to `Vehicle` entity
- Populated from `vehicle_model` catalog on migration
- New vehicles get rate from `createVehicleDto.DailyRate`

### 2. Bulk Update Logic

**Before:** Updated `vehicle_model` catalog (affected all companies)
```csharp
vehicleModel.DailyRate = request.DailyRate; // Global catalog
```

**After:** Updates individual vehicles (company-specific)
```csharp
vehicle.DailyRate = request.DailyRate; // Per-vehicle, company-scoped
```

### 3. Company Scoping

Bulk updates now filter by company:
1. Filter models to only those used by company's vehicles
2. Find vehicles matching those models AND company
3. Update ONLY those vehicles
4. Other companies' vehicles unaffected

### 4. Dashboard Display

**GetModelsGroupedByCategory** when `companyId` provided:
1. Filter models to company's vehicles
2. Aggregate rates from company's vehicles (AVG)
3. Display aggregated rate for each model
4. Shows "different" if rates vary within company

## File Changes

### Backend
- ✅ `Vehicle.cs` - Added `DailyRate` property
- ✅ `ModelsController.BulkUpdateDailyRate` - Updates vehicles, not catalog
- ✅ `ModelsController.GetModelsGroupedByCategory` - Aggregates vehicle rates
- ✅ `VehiclesController` - All queries use fallback logic
- ✅ `CompanyManagementService` - Uses fallback logic

### Database
- ✅ `add_daily_rate_to_vehicles.sql` - Migration script ready

## Flow Example

**Company A has:**
- FORD MUSTANG 2025: $150/day
- FORD MUSTANG 2024: $140/day

**Company B has:**
- FORD MUSTANG 2025: $180/day

**Dashboard for Company A shows:**
- 2025: $150
- 2024: $140

**Dashboard for Company B shows:**
- 2025: $180

**Bulk update on Company A (2025 → $155):**
- Only Company A's 2025 vehicles updated to $155
- Company B's 2025 vehicles stay at $180
- `vehicle_model` catalog unchanged

## Testing Steps

1. Run `add_daily_rate_to_vehicles.sql` migration
2. Restart API
3. Open Company A dashboard → see rates
4. Update Category/Make/Model/Year rate
5. Verify only Company A's vehicles updated
6. Switch to Company B → verify unchanged rates

## Benefits

✅ **Company isolation** - Each company manages their own rates
✅ **Bulk efficiency** - Update all vehicles of a model at once
✅ **Global defaults** - Catalog provides fallback for new vehicles
✅ **Flexibility** - Can override per-vehicle if needed
✅ **No data duplication** - Rates stored once per vehicle

