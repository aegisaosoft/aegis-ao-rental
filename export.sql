SELECT 
	ev.Tag,
	ev.State,
	ev.Model,
	ev.Make,
	ev.Color,
	ev.ProviderVehicleId as year,
	'1d8d434e-3ce4-462a-87a5-28b8e7fb072b' as category_id,
    'e1689b8a-1766-4b3f-adc1-b5b6c02f2f5a' as company_id,
    ev.LicensePlate as license_plate,
    ev.State 
FROM ExternalVehicles ev
WHERE ev.LicensePlate IS NOT NULL 
AND ev.Make IS NOT NULL
AND ev.Model IS NOT NULL;