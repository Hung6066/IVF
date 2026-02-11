import pdfplumber

with pdfplumber.open('d:/Pr.Net/IVF/test_output.pdf') as pdf:
    page = pdf.pages[0]

    # Group chars by approximate Y position
    from collections import defaultdict
    lines = defaultdict(list)
    for c in page.chars:
        y = round(c['top'], 0)
        lines[y].append(c)

    # Show first 30 lines with font info
    count = 0
    for y in sorted(lines.keys()):
        chars = sorted(lines[y], key=lambda c: c['x0'])
        text = ''.join(c['text'] for c in chars)
        sizes = set(round(c['size'], 1) for c in chars)
        fonts = set(c.get('fontname', '?') for c in chars)
        x_start = chars[0]['x0']
        print(f"Y={y:7.1f} X={x_start:6.1f} size={sizes} font={fonts}")
        print(f"         {text[:90]}")
        count += 1
        if count >= 35:
            break
