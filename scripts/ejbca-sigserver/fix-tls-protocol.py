#!/usr/bin/env python3
"""Fix TLS 1.3 in SignServer WildFly functions-appserver to avoid Firefox SSL_ERROR_INTERNAL_ERROR_ALERT"""
path = "/opt/keyfactor/bin/internal/functions-appserver"
with open(path, "r") as f:
    content = f.read()
old = '[\\\"TLSv1.3\\\",\\\"TLSv1.2\\\"]'
new = '[\\\"TLSv1.2\\\"]'
count = content.count(old)
print(f"{count} occurrences found")
content = content.replace(old, new)
with open(path, "w") as f:
    f.write(content)
print("Done - TLSv1.3 removed from functions-appserver")
