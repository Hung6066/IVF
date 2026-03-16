import re, sys

with open("/tmp/profiles/entityprofile_IVF-TSA-EEProfile-6002.xml") as f:
    content = f.read()

# Remove <void method="put"><int>N</int><string>V</string></void> blocks (broken SAN integer keys)
pattern = r'\s*<void method="put">\s*<int>\d+</int>\s*<string>[^<]*</string>\s*</void>'
cleaned = re.sub(pattern, "", content)

# Add empty SUBJECTALTNAME_FIELDORDER before PROFILETYPE
insert = '  <void method="put">\n   <string>SUBJECTALTNAME_FIELDORDER</string>\n   <object class="java.util.ArrayList"/>\n  </void>\n  '
needle = '  <void method="put">\n   <string>PROFILETYPE</string>'
if needle in cleaned:
    cleaned = cleaned.replace(needle, insert + '<void method="put">\n   <string>PROFILETYPE</string>', 1)
    print("Needle found and replaced")
else:
    print("ERROR: needle not found! Searching for nearby text...")
    idx = cleaned.find("PROFILETYPE")
    if idx >= 0:
        print("PROFILETYPE context:", repr(cleaned[max(0,idx-80):idx+40]))

with open("/tmp/tsa-fixed.xml", "w") as f:
    f.write(cleaned)

print("Done. Lines:", cleaned.count("\n"))
# Show last 30 lines
lines = cleaned.splitlines()
print("--- Last 30 lines ---")
for l in lines[-30:]:
    print(l)
