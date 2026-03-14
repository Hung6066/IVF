# SQL Injection
curl -s -o /dev/null -w "SQLi: %{http_code}\n" "https://natra.site/api/patients?q=%27+UNION+SELECT+table_name+FROM+information_schema.tables+--"

# XSS
curl -s -o /dev/null -w "XSS: %{http_code}\n" "https://natra.site/api/patients?q=%3Cscript%3Ealert%28document.cookie%29%3C%2Fscript%3E"

# Path Traversal
curl -s -o /dev/null -w "PathTraversal: %{http_code}\n" "https://natra.site/api/files?path=../../../etc/passwd"

# Command Injection
curl -s -o /dev/null -w "CmdInjection: %{http_code}\n" "https://natra.site/api/patients?q=%3B+cat+%2Fetc%2Fpasswd"

# Known Bad Bot (sqlmap UA)
curl -s -o /dev/null -w "BadBot(sqlmap): %{http_code}\n" -A "sqlmap/1.7.9#stable (https://sqlmap.org)" "https://natra.site/api/patients"

# Scanner Tool (burpsuite UA)
curl -s -o /dev/null -w "Scanner(burp): %{http_code}\n" -A "BurpSuite/2023.12" "https://natra.site/api/patients"

# AI Crawler (GPTBot UA)
curl -s -o /dev/null -w "AICrawler(GPT): %{http_code}\n" -A "Mozilla/5.0 (compatible; GPTBot/1.0; +https://openai.com/gptbot)" "https://natra.site/api/patients"

# SSRF
curl -s -o /dev/null -w "SSRF: %{http_code}\n" "https://natra.site/api/patients?url=http%3A%2F%2F169.254.169.254%2Flatest%2Fmeta-data%2F"

# NoSQL Injection
curl -s -o /dev/null -w "NoSQLi: %{http_code}\n" "https://natra.site/api/patients?filter=%7B%22%24where%22%3A%221%3D1%22%7D"

# SSTI
curl -s -o /dev/null -w "SSTI: %{http_code}\n" "https://natra.site/api/patients?q=%7B%7B7*7%7D%7D"

# XXE (POST with XML body)
curl -s -o /dev/null -w "XXE: %{http_code}\n" -X POST "https://natra.site/api/patients" \
  -H "Content-Type: application/xml" \
  -d '<?xml version="1.0"?><!DOCTYPE foo SYSTEM "file:///etc/passwd"><foo/>'

# Open Redirect
curl -s -o /dev/null -w "OpenRedirect: %{http_code}\n" "https://natra.site/api/auth/logout?redirect=http%3A%2F%2Fevil.com"

# PHP Injection
curl -s -o /dev/null -w "PHPInjection: %{http_code}\n" "https://natra.site/api/patients?q=%3C%3Fphp+system%28%24_GET%5B%27cmd%27%5D%29%3B+%3F%3E"

# Non-Standard HTTP Method
curl -s -o /dev/null -w "TRACE: %{http_code}\n" -X TRACE "https://natra.site/api/patients"