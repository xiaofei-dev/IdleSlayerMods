from __future__ import annotations

import html
import re
import zipfile
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate, Frame, Image, ListFlowable, ListItem, PageBreak,
    PageTemplate, Paragraph, Spacer, Table, TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf"
VERSION = "1.1.0"
PAGE_WIDTH, PAGE_HEIGHT = A4

NAVY = colors.HexColor("#17233A")
PURPLE = colors.HexColor("#7839C8")
MAGENTA = colors.HexColor("#CF2B9B")
INK = colors.HexColor("#202735")
MUTED = colors.HexColor("#657086")
LINE = colors.HexColor("#CBD7E5")


def register_fonts() -> tuple[str, str, str, str]:
    regular = Path(r"C:\Windows\Fonts\segoeui.ttf")
    if not regular.exists():
        return "Helvetica", "Helvetica-Bold", "Helvetica-Oblique", "Courier"
    bold = Path(r"C:\Windows\Fonts\segoeuib.ttf")
    italic = Path(r"C:\Windows\Fonts\segoeuii.ttf")
    mono = Path(r"C:\Windows\Fonts\consola.ttf")
    pdfmetrics.registerFont(TTFont("AdventurerSans", str(regular)))
    pdfmetrics.registerFont(TTFont("AdventurerSans-Bold", str(bold if bold.exists() else regular)))
    pdfmetrics.registerFont(TTFont("AdventurerSans-Italic", str(italic if italic.exists() else regular)))
    mono_name = "Courier"
    if mono.exists():
        mono_name = "AdventurerMono"
        pdfmetrics.registerFont(TTFont(mono_name, str(mono)))
    return "AdventurerSans", "AdventurerSans-Bold", "AdventurerSans-Italic", mono_name


FONT, FONT_BOLD, FONT_ITALIC, FONT_MONO = register_fonts()
BASE = getSampleStyleSheet()
STYLES = {
    "body": ParagraphStyle("Body", parent=BASE["BodyText"], fontName=FONT,
        fontSize=9.3, leading=14.1, textColor=INK, spaceAfter=6),
    "h1": ParagraphStyle("H1", parent=BASE["Heading1"], fontName=FONT_BOLD,
        fontSize=21, leading=26, textColor=NAVY, spaceBefore=5, spaceAfter=11,
        keepWithNext=True),
    "h2": ParagraphStyle("H2", parent=BASE["Heading2"], fontName=FONT_BOLD,
        fontSize=14.3, leading=19, textColor=PURPLE, spaceBefore=11, spaceAfter=6,
        keepWithNext=True),
    "h3": ParagraphStyle("H3", parent=BASE["Heading3"], fontName=FONT_BOLD,
        fontSize=11, leading=15, textColor=NAVY, spaceBefore=8, spaceAfter=4,
        keepWithNext=True),
    "code": ParagraphStyle("Code", fontName=FONT_MONO, fontSize=7.5, leading=10.8,
        leftIndent=7, rightIndent=7, textColor=colors.HexColor("#E7EEF8"),
        backColor=NAVY, borderPadding=7, spaceBefore=4, spaceAfter=8),
    "cell": ParagraphStyle("Cell", fontName=FONT, fontSize=7.8, leading=10.6,
        textColor=INK),
    "head": ParagraphStyle("Head", fontName=FONT_BOLD, fontSize=7.9, leading=10.6,
        textColor=colors.white),
    "cover_title": ParagraphStyle("CoverTitle", fontName=FONT_BOLD, fontSize=29,
        leading=34, alignment=TA_CENTER, textColor=NAVY, spaceAfter=9),
    "cover_subtitle": ParagraphStyle("CoverSubtitle", fontName=FONT, fontSize=13,
        leading=18, alignment=TA_CENTER, textColor=MUTED, spaceAfter=9),
    "cover_meta": ParagraphStyle("CoverMeta", fontName=FONT, fontSize=9.5,
        leading=14, alignment=TA_CENTER, textColor=MUTED),
}


def markup(value: str) -> str:
    value = html.escape(value, quote=False)
    value = re.sub(r"\[([^\]]+)\]\((https?://[^)]+)\)",
        r'<link href="\2" color="#7839C8"><u>\1</u></link>', value)
    value = re.sub(r"\[([^\]]+)\]\((?!https?://)[^)]+\)", r"\1", value)
    value = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", value)
    value = re.sub(r"`([^`]+)`", rf'<font name="{FONT_MONO}">\1</font>', value)
    return value


def para(value: str, style: str = "body") -> Paragraph:
    return Paragraph(markup(value), STYLES[style])


def markdown_flowables(path: Path, *, omit_title: bool = False, omit_backlink: bool = True):
    lines = path.read_text(encoding="utf-8").splitlines()
    flow, i, skipped = [], 0, False
    while i < len(lines):
        text = lines[i].rstrip()
        stripped = text.strip()
        if not stripped:
            i += 1
            continue
        if omit_backlink and stripped.startswith("[Back to the Complete Manual]"):
            i += 1
            continue
        if stripped.startswith("```"):
            code = []
            i += 1
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code.append(lines[i].rstrip())
                i += 1
            rendered = "<br/>".join(html.escape(x or " ", quote=False) for x in code)
            flow.append(Paragraph(rendered, STYLES["code"]))
            i += 1
            continue
        if stripped.startswith("# "):
            if omit_title and not skipped:
                skipped = True
            else:
                flow.append(para(stripped[2:], "h1"))
            i += 1
            continue
        if stripped.startswith("## "):
            flow.append(para(stripped[3:], "h2")); i += 1; continue
        if stripped.startswith("### "):
            flow.append(para(stripped[4:], "h3")); i += 1; continue
        if stripped.startswith("|"):
            raw = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                raw.append([c.strip() for c in lines[i].strip().strip("|").split("|")])
                i += 1
            if len(raw) > 1 and all(re.fullmatch(r":?-{3,}:?", c) for c in raw[1]):
                del raw[1]
            rows = []
            for row_no, row in enumerate(raw):
                style = STYLES["head"] if row_no == 0 else STYLES["cell"]
                rows.append([Paragraph(markup(c), style) for c in row])
            widths = [165 * mm / len(rows[0])] * len(rows[0])
            table = Table(rows, colWidths=widths, repeatRows=1, hAlign="LEFT")
            table.setStyle(TableStyle([
                ("BACKGROUND", (0, 0), (-1, 0), NAVY),
                ("BOX", (0, 0), (-1, -1), .6, LINE),
                ("INNERGRID", (0, 0), (-1, -1), .35, LINE),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 5),
                ("RIGHTPADDING", (0, 0), (-1, -1), 5),
                ("TOPPADDING", (0, 0), (-1, -1), 5),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
            ]))
            flow.extend([table, Spacer(1, 7)])
            continue
        marker = re.match(r"^\s*([-*]|\d+\.)\s+", text)
        if marker:
            numbered = marker.group(1)[0].isdigit()
            items = []
            pattern = r"^\s*\d+\.\s+" if numbered else r"^\s*[-*]\s+"
            while i < len(lines) and re.match(pattern, lines[i]):
                item = re.sub(pattern, "", lines[i].strip())
                i += 1
                while i < len(lines) and lines[i].strip() and not re.match(
                    r"^(#{1,3}\s|\s*[-*]\s+|\s*\d+\.\s+|\||```)", lines[i]):
                    item += " " + lines[i].strip(); i += 1
                items.append(ListItem(para(item), leftIndent=14))
            flow.append(ListFlowable(items, bulletType="1" if numbered else "bullet",
                leftIndent=22 if numbered else 17, bulletFontName=FONT_BOLD,
                bulletFontSize=8, spaceAfter=6))
            continue
        parts = [stripped]
        i += 1
        while i < len(lines) and lines[i].strip() and not re.match(
            r"^(#{1,3}\s|\s*[-*]\s+|\s*\d+\.\s+|\||```)", lines[i]):
            parts.append(lines[i].strip()); i += 1
        flow.append(para(" ".join(parts)))
    return flow


class Template(BaseDocTemplate):
    def __init__(self, target: Path, document_name: str):
        self.document_name = document_name
        super().__init__(str(target), pagesize=A4, leftMargin=22*mm, rightMargin=22*mm,
            topMargin=20*mm, bottomMargin=18*mm, title=document_name, author="Tashi",
            subject="AutoAdventurer documentation for Idle Slayer")
        frame = Frame(self.leftMargin, self.bottomMargin, self.width, self.height, id="normal")
        self.addPageTemplates(PageTemplate(id="main", frames=[frame], onPage=self.decorate))

    def decorate(self, canvas, doc):
        if doc.page == 1:
            return
        canvas.saveState(); canvas.setStrokeColor(LINE); canvas.setLineWidth(.5)
        canvas.line(22*mm, PAGE_HEIGHT-13*mm, PAGE_WIDTH-22*mm, PAGE_HEIGHT-13*mm)
        canvas.setFont(FONT, 7.5); canvas.setFillColor(MUTED)
        canvas.drawString(22*mm, PAGE_HEIGHT-10*mm, "AutoAdventurer")
        canvas.drawRightString(PAGE_WIDTH-22*mm, PAGE_HEIGHT-10*mm, self.document_name)
        canvas.line(22*mm, 12*mm, PAGE_WIDTH-22*mm, 12*mm)
        canvas.drawString(22*mm, 8*mm, "Idle Slayer community mod documentation")
        canvas.drawRightString(PAGE_WIDTH-22*mm, 8*mm, f"Page {doc.page}")
        canvas.restoreState()


def cover(title: str, subtitle: str):
    flow = []
    banner = ROOT / "Assets" / "banner.png"
    if banner.exists():
        flow.extend([Image(str(banner), width=165*mm, height=55*mm), Spacer(1, 17*mm)])
    else:
        flow.append(Spacer(1, 50*mm))
    flow.extend([
        Paragraph("AutoAdventurer", STYLES["cover_title"]),
        Paragraph(title, STYLES["cover_subtitle"]), Spacer(1, 8*mm),
        Table([[""]], colWidths=[45*mm], rowHeights=[1.2*mm],
            style=[("BACKGROUND", (0, 0), (-1, -1), MAGENTA)]),
        Spacer(1, 8*mm), Paragraph(subtitle, STYLES["cover_meta"]),
        Paragraph(f"Version {VERSION}  |  Tashi", STYLES["cover_meta"]), PageBreak(),
    ])
    return flow


def build_user_guide() -> Path:
    target = OUTPUT / f"AutoAdventurer-User-Guide-{VERSION}.pdf"
    story = cover("User Guide", "Installation, quick start, controls, and everyday use")
    story.extend(markdown_flowables(ROOT / "USER_GUIDE.md", omit_title=True))
    Template(target, "User Guide").build(story)
    return target


def build_manual() -> Path:
    target = OUTPUT / f"AutoAdventurer-Complete-Manual-{VERSION}.pdf"
    story = cover("Complete Manual",
        "Quest automation, travel, Rage, Boost, events, configuration, logging, and troubleshooting")
    story.extend(markdown_flowables(ROOT / "MANUAL.md", omit_title=True))
    for chapter in sorted((ROOT / "docs").glob("[0-9][0-9]-*.md")):
        story.append(PageBreak())
        story.extend(markdown_flowables(chapter))
    Template(target, "Complete Manual").build(story)
    return target


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    guide, manual = build_user_guide(), build_manual()
    package = OUTPUT / f"AutoAdventurer-Documentation-{VERSION}.zip"
    with zipfile.ZipFile(package, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.write(guide, guide.name)
        archive.write(manual, manual.name)
    for path in (guide, manual, package):
        print(path)


if __name__ == "__main__":
    main()
