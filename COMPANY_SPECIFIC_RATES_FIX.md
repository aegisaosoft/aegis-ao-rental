# Company-Specific Rate Updates - Fix

## Problem

1. Dashboard showed ALL models from database, not just company's vehicles
2. Bulk rate updates affected ALL companies globally (vehicle_model is shared catalog)
3. Needed to show only models that belong to company's vehicles
4. Needed to update rates only for models used by current company's vehicles

## Solution

### 1. Dashboard Filtering (GetModelsGroupedByCategory)

**Before:** Filtered by make/model pairs (not including year)
```csharp
vehiclePairs.Contains($"{make}|{model}")
```

**After:** Filtered by make/model/year triplets (exact match)
```csharp
vehicleTriplets.Contains($"{make}|{model}|{year}")
```

This ensures only models that exactly match company's vehicles are shown.

### 2. Bulk Update Filtering (BulkUpdateDailyRate)

**Before:** Updated all models matching filters globally
```csharp
IQueryable<Model> query = _context.Models;
// Filters applied to all models
```

**After:** First filters by company vehicles, then applies additional filters
```csharp
if (request.CompanyId.HasValue)
{
    // Get model IDs from vehicles of this company
    var companyModelIds = await _context.Vehicles
        .Where(v => v.CompanyId == request.CompanyId.Value && v.VehicleModelId != null)
        .Select(v => v.VehicleModelId!.Value)
        .Distinct()
        .ToListAsync();
    
    // Filter to only models used by this company's vehicles
    query = query.Where(m => companyModelIds.Contains(m.Id));
}
```

### 3. DTO Update

**Added:** `CompanyId` field to `BulkUpdateModelDailyRateDto`
```csharp
public Guid? CompanyId { get; set; } // Filter to only update models for vehicles of this company
```

### 4. Frontend Updates

**Added:** `companyId: currentCompanyId` to all bulk update calls:
- Category update
- Make update
- Model update
- Year update

## Flow

1. **Dashboard Display:**
   - Frontend calls `/Models/grouped-by-category?companyId=XXX`
   - Backend filters models to only those matching company vehicles (make/model/year)
   - Returns only company-specific models with rates

2. **Bulk Rate Update:**
   - Frontend sends `{ dailyRate: 100, categoryId: ..., companyId: XXX }`
   - Backend first filters to company's vehicle models
   - Then applies category/make/model/year filters
   - Updates only vehicle_model entries for those models
   - Creates new entries if needed (but only for company's models)

## Result

✅ Dashboard shows ONLY models that belong to current company's vehicles
✅ Rate updates ONLY affect models used by current company's vehicles
✅ Other companies' rates remain unchanged
✅ Exact matching by make/model/year ensures accuracy

## Note

The `vehicle_model` catalog is still shared globally, but updates are now scoped to company-specific models. This means:
- If Company A has "FORD MUSTANG 2025" and updates its rate
- Only the vehicle_model entry for "FORD MUSTANG 2025" is updated
- If Company B also has "FORD MUSTANG 2025", they'll see the updated rate
- This is by design - it's a shared catalog template

If truly company-specific rates are needed, we'd need to add `company_id` to `vehicle_model` table or store rates directly on `vehicles` table.

