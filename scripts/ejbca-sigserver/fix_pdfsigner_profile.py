import re

with open("/tmp/pdfsigner-ee.xml") as f:
    content = f.read()

pattern = r'\s*<void method="put">\s*<int>\d+</int>\s*<string>[^<]*</string>\s*</void>'
cleaned = re.sub(pattern, "", content)

insert = '  <void method="put">\n   <string>SUBJECTALTNAME_FIELDORDER</string>\n   <object class="java.util.ArrayList"/>\n  </void>\n  '
needle = '  <void method="put">\n   <string>PROFILETYPE</string>'
if needle in cleaned:
    cleaned = cleaned.replace(needle, insert + '<void method="put">\n   <string>PROFILETYPE</string>', 1)
    print("PDFSigner EE profile fixed")
else:
    print("ERROR: needle not found!")

with open("/tmp/ee-only/entityprofile_IVF-PDFSigner-EEProfile-6001.xml", "w") as f:
    f.write(cleaned)
print("Done.")
