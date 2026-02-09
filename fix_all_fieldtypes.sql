-- Comprehensive fix for ALL remaining FieldType = 0 values
-- This script will check each field and assign the correct type

-- Step 1: Check current state
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    "OptionsJson",
    LENGTH("OptionsJson") as "OptionsLength"
FROM "FormFields"
WHERE "FieldType" = 0
ORDER BY "DisplayOrder";

-- Step 2: Update fields with options to appropriate types
-- Fields with options and specific labels

-- Dropdown fields (have options, label contains dropdown/danh sách/chọn)
UPDATE "FormFields"
SET "FieldType" = 8
WHERE "FieldType" = 0 
  AND ("OptionsJson" IS NOT NULL AND "OptionsJson" != '' AND "OptionsJson" != '[]')
  AND (
    LOWER("Label") LIKE '%dropdown%' 
    OR LOWER("Label") LIKE '%danh sách%'
    OR LOWER("Label") LIKE '%chọn%'
    OR LOWER("FieldKey") LIKE '%dropdown%'
    OR LOWER("FieldKey") LIKE '%select%'
  );

-- Radio fields (have options, label contains radio/nút)
UPDATE "FormFields"
SET "FieldType" = 10
WHERE "FieldType" = 0 
  AND ("OptionsJson" IS NOT NULL AND "OptionsJson" != '' AND "OptionsJson" != '[]')
  AND (
    LOWER("Label") LIKE '%radio%' 
    OR LOWER("Label") LIKE '%nút%'
    OR LOWER("FieldKey") LIKE '%radio%'
  );

-- Tags fields (have options, label contains tag)
UPDATE "FormFields"
SET "FieldType" = 16
WHERE "FieldType" = 0 
  AND (
    LOWER("Label") LIKE '%tag%'
    OR LOWER("FieldKey") LIKE '%tag%'
  );

-- Checkbox fields (label contains checkbox)
UPDATE "FormFields"
SET "FieldType" = 11
WHERE "FieldType" = 0 
  AND (
    LOWER("Label") LIKE '%checkbox%'
    OR LOWER("FieldKey") LIKE '%checkbox%'
  );

-- Step 3: For any remaining fields with options but no specific label, default to Dropdown
UPDATE "FormFields"
SET "FieldType" = 8
WHERE "FieldType" = 0 
  AND ("OptionsJson" IS NOT NULL AND "OptionsJson" != '' AND "OptionsJson" != '[]');

-- Step 4: For any remaining fields without options, default to Text
UPDATE "FormFields"
SET "FieldType" = 1
WHERE "FieldType" = 0;

-- Step 5: Verify no more 0 values exist
SELECT 
    "Id",
    "Label",
    "FieldKey",
    "FieldType",
    CASE "FieldType"
        WHEN 1 THEN 'Text'
        WHEN 2 THEN 'TextArea'
        WHEN 3 THEN 'Number'
        WHEN 4 THEN 'Decimal'
        WHEN 5 THEN 'Date'
        WHEN 8 THEN 'Dropdown'
        WHEN 9 THEN 'MultiSelect'
        WHEN 10 THEN 'Radio'
        WHEN 11 THEN 'Checkbox'
        WHEN 12 THEN 'FileUpload'
        WHEN 13 THEN 'Rating'
        WHEN 16 THEN 'Tags'
        ELSE 'Unknown (' || "FieldType" || ')'
    END AS "FieldTypeName",
    "OptionsJson"
FROM "FormFields"
ORDER BY "DisplayOrder";

-- Final check: Count by type
SELECT 
    "FieldType",
    CASE "FieldType"
        WHEN 1 THEN 'Text'
        WHEN 2 THEN 'TextArea'
        WHEN 8 THEN 'Dropdown'
        WHEN 10 THEN 'Radio'
        WHEN 11 THEN 'Checkbox'
        WHEN 16 THEN 'Tags'
        ELSE 'Other'
    END AS "TypeName",
    COUNT(*) as "Count"
FROM "FormFields"
GROUP BY "FieldType"
ORDER BY "FieldType";
