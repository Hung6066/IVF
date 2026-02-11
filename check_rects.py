import pdfplumber

with pdfplumber.open('d:/Pr.Net/IVF/test_output.pdf') as pdf:
    page = pdf.pages[0]
    rects = page.rects or []
    lines = page.lines or []
    print(f'Rectangles: {len(rects)}, Lines: {len(lines)}')
    for r in rects[:25]:
        fill = r.get('non_stroking_color', r.get('fill'))
        stroke = r.get('stroking_color', r.get('stroke_color'))
        x0, top, w, h = r['x0'], r['top'], r['width'], r['height']
        print(
            f'  rect ({x0:.0f},{top:.0f}) {w:.0f}x{h:.0f} fill={fill} stroke={stroke}')
