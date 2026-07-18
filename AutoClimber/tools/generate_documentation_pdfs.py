from __future__ import annotations

import html
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfbase import pdfmetrics
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    Image,
    KeepTogether,
    ListFlowable,
    ListItem,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf"
PAGE_WIDTH, PAGE_HEIGHT = A4

NAVY = colors.HexColor("#17233A")
BLUE = colors.HexColor("#2F73C9")
CYAN = colors.HexColor("#36B6D9")
INK = colors.HexColor("#202735")
MUTED = colors.HexColor("#657086")
PALE = colors.HexColor("#EEF4FA")
LINE = colors.HexColor("#CBD7E5")


def register_fonts() -> tuple[str, str, str, str]:
    candidates = [
        Path(r"C:\Windows\Fonts\segoeui.ttf"),
        Path(r"C:\Windows\Fonts\arial.ttf"),
    ]
    regular_path = next((p for p in candidates if p.exists()), None)
    if regular_path is None:
        return "Helvetica", "Helvetica-Bold", "Helvetica-Oblique", "Courier"

    family = "AutoClimberSans"
    regular = str(regular_path)
    bold_path = Path(r"C:\Windows\Fonts\segoeuib.ttf")
    italic_path = Path(r"C:\Windows\Fonts\segoeuii.ttf")
    mono_path = Path(r"C:\Windows\Fonts\consola.ttf")
    pdfmetrics.registerFont(TTFont(family, regular))
    pdfmetrics.registerFont(
        TTFont(f"{family}-Bold", str(bold_path if bold_path.exists() else regular_path))
    )
    pdfmetrics.registerFont(
        TTFont(f"{family}-Italic", str(italic_path if italic_path.exists() else regular_path))
    )
    mono = "AutoClimberMono"
    if mono_path.exists():
        pdfmetrics.registerFont(TTFont(mono, str(mono_path)))
    else:
        mono = "Courier"
    return family, f"{family}-Bold", f"{family}-Italic", mono


FONT, FONT_BOLD, FONT_ITALIC, FONT_MONO = register_fonts()


def make_styles():
    base = getSampleStyleSheet()
    return {
        "body": ParagraphStyle(
            "Body",
            parent=base["BodyText"],
            fontName=FONT,
            fontSize=9.4,
            leading=14.2,
            textColor=INK,
            spaceAfter=6,
        ),
        "h1": ParagraphStyle(
            "H1",
            parent=base["Heading1"],
            fontName=FONT_BOLD,
            fontSize=22,
            leading=27,
            textColor=NAVY,
            spaceBefore=5,
            spaceAfter=12,
            keepWithNext=True,
        ),
        "h2": ParagraphStyle(
            "H2",
            parent=base["Heading2"],
            fontName=FONT_BOLD,
            fontSize=14.5,
            leading=19,
            textColor=BLUE,
            spaceBefore=11,
            spaceAfter=6,
            keepWithNext=True,
        ),
        "h3": ParagraphStyle(
            "H3",
            parent=base["Heading3"],
            fontName=FONT_BOLD,
            fontSize=11,
            leading=15,
            textColor=NAVY,
            spaceBefore=8,
            spaceAfter=4,
            keepWithNext=True,
        ),
        "code": ParagraphStyle(
            "Code",
            fontName=FONT_MONO,
            fontSize=7.7,
            leading=11.2,
            leftIndent=7,
            rightIndent=7,
            textColor=colors.HexColor("#E7EEF8"),
            backColor=NAVY,
            borderPadding=7,
            spaceBefore=4,
            spaceAfter=8,
        ),
        "table": ParagraphStyle(
            "TableCell",
            fontName=FONT,
            fontSize=8.2,
            leading=11.2,
            textColor=INK,
        ),
        "table_head": ParagraphStyle(
            "TableHead",
            fontName=FONT_BOLD,
            fontSize=8.3,
            leading=11.2,
            textColor=colors.white,
        ),
        "cover_title": ParagraphStyle(
            "CoverTitle",
            fontName=FONT_BOLD,
            fontSize=30,
            leading=35,
            alignment=TA_CENTER,
            textColor=NAVY,
            spaceAfter=9,
        ),
        "cover_subtitle": ParagraphStyle(
            "CoverSubtitle",
            fontName=FONT,
            fontSize=13,
            leading=18,
            alignment=TA_CENTER,
            textColor=MUTED,
            spaceAfter=9,
        ),
        "cover_meta": ParagraphStyle(
            "CoverMeta",
            fontName=FONT,
            fontSize=9.5,
            leading=14,
            alignment=TA_CENTER,
            textColor=MUTED,
        ),
    }


STYLES = make_styles()


def inline_markup(text: str) -> str:
    text = html.escape(text, quote=False)
    text = re.sub(
        r"\[([^\]]+)\]\((https?://[^)]+)\)",
        r'<link href="\2" color="#2F73C9"><u>\1</u></link>',
        text,
    )
    text = re.sub(r"\[([^\]]+)\]\((?!https?://)[^)]+\)", r"\1", text)
    text = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", text)
    text = re.sub(r"`([^`]+)`", rf'<font name="{FONT_MONO}">\1</font>', text)
    return text


def paragraph(text: str, style="body") -> Paragraph:
    return Paragraph(inline_markup(text), STYLES[style])


def markdown_to_flowables(path: Path, *, omit_title=False, omit_backlink=True):
    lines = path.read_text(encoding="utf-8").splitlines()
    flow = []
    i = 0
    first_heading_skipped = False

    while i < len(lines):
        line = lines[i].rstrip()
        stripped = line.strip()
        if not stripped:
            i += 1
            continue
        if omit_backlink and stripped.startswith("[Back to the Complete Manual]"):
            i += 1
            continue
        if stripped.startswith("```"):
            code_lines = []
            i += 1
            while i < len(lines) and not lines[i].strip().startswith("```"):
                code_lines.append(lines[i].rstrip())
                i += 1
            code_text = "<br/>".join(
                html.escape(x if x else " ", quote=False) for x in code_lines
            )
            flow.append(Paragraph(code_text, STYLES["code"]))
            i += 1
            continue
        if stripped.startswith("# "):
            if omit_title and not first_heading_skipped:
                first_heading_skipped = True
            else:
                flow.append(paragraph(stripped[2:], "h1"))
            i += 1
            continue
        if stripped.startswith("## "):
            flow.append(paragraph(stripped[3:], "h2"))
            i += 1
            continue
        if stripped.startswith("### "):
            flow.append(paragraph(stripped[4:], "h3"))
            i += 1
            continue
        if stripped.startswith("|"):
            table_lines = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                table_lines.append(lines[i].strip())
                i += 1
            rows = [
                [cell.strip() for cell in row.strip("|").split("|")]
                for row in table_lines
            ]
            if len(rows) > 1 and all(re.fullmatch(r":?-{3,}:?", c) for c in rows[1]):
                del rows[1]
            cells = []
            for row_index, row in enumerate(rows):
                style = STYLES["table_head"] if row_index == 0 else STYLES["table"]
                cells.append([Paragraph(inline_markup(c), style) for c in row])
            widths = [165 * mm / len(cells[0])] * len(cells[0])
            table = Table(cells, colWidths=widths, repeatRows=1, hAlign="LEFT")
            table.setStyle(
                TableStyle(
                    [
                        ("BACKGROUND", (0, 0), (-1, 0), NAVY),
                        ("BOX", (0, 0), (-1, -1), 0.6, LINE),
                        ("INNERGRID", (0, 0), (-1, -1), 0.35, LINE),
                        ("BACKGROUND", (0, 1), (-1, -1), colors.white),
                        ("VALIGN", (0, 0), (-1, -1), "TOP"),
                        ("LEFTPADDING", (0, 0), (-1, -1), 6),
                        ("RIGHTPADDING", (0, 0), (-1, -1), 6),
                        ("TOPPADDING", (0, 0), (-1, -1), 5),
                        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
                    ]
                )
            )
            flow.extend([table, Spacer(1, 7)])
            continue
        if re.match(r"^[-*] ", stripped):
            items = []
            while i < len(lines) and re.match(r"^\s*[-*] ", lines[i]):
                item_text = re.sub(r"^\s*[-*] ", "", lines[i].strip())
                i += 1
                while i < len(lines) and lines[i].strip() and not re.match(
                    r"^(#{1,3} |\s*[-*] |\d+\. |\||```)", lines[i]
                ):
                    item_text += " " + lines[i].strip()
                    i += 1
                items.append(ListItem(paragraph(item_text), leftIndent=12))
            flow.append(
                ListFlowable(
                    items,
                    bulletType="bullet",
                    start="circle",
                    leftIndent=17,
                    bulletFontName=FONT,
                    bulletFontSize=7,
                    spaceAfter=6,
                )
            )
            continue
        if re.match(r"^\d+\. ", stripped):
            items = []
            while i < len(lines) and re.match(r"^\s*\d+\. ", lines[i]):
                item_text = re.sub(r"^\s*\d+\. ", "", lines[i].strip())
                i += 1
                while i < len(lines) and lines[i].strip() and not re.match(
                    r"^(#{1,3} |\s*[-*] |\d+\. |\||```)", lines[i]
                ):
                    item_text += " " + lines[i].strip()
                    i += 1
                items.append(ListItem(paragraph(item_text), leftIndent=15))
            flow.append(
                ListFlowable(
                    items,
                    bulletType="1",
                    leftIndent=22,
                    bulletFontName=FONT_BOLD,
                    bulletFontSize=8,
                    spaceAfter=6,
                )
            )
            continue

        parts = [stripped]
        i += 1
        while i < len(lines) and lines[i].strip() and not re.match(
            r"^(#{1,3} |\s*[-*] |\d+\. |\||```)", lines[i]
        ):
            parts.append(lines[i].strip())
            i += 1
        flow.append(paragraph(" ".join(parts)))
    return flow


class DocumentationTemplate(BaseDocTemplate):
    def __init__(self, filename: Path, document_name: str):
        self.document_name = document_name
        super().__init__(
            str(filename),
            pagesize=A4,
            leftMargin=22 * mm,
            rightMargin=22 * mm,
            topMargin=20 * mm,
            bottomMargin=18 * mm,
            title=document_name,
            author="Tashi",
            subject="AutoClimber documentation for Idle Slayer",
        )
        frame = Frame(
            self.leftMargin,
            self.bottomMargin,
            self.width,
            self.height,
            id="normal",
        )
        self.addPageTemplates(PageTemplate(id="main", frames=[frame], onPage=self.decorate))

    def decorate(self, canvas, doc):
        if doc.page == 1:
            return
        canvas.saveState()
        canvas.setStrokeColor(LINE)
        canvas.setLineWidth(0.5)
        canvas.line(22 * mm, PAGE_HEIGHT - 13 * mm, PAGE_WIDTH - 22 * mm, PAGE_HEIGHT - 13 * mm)
        canvas.setFont(FONT, 7.5)
        canvas.setFillColor(MUTED)
        canvas.drawString(22 * mm, PAGE_HEIGHT - 10 * mm, "AutoClimber")
        canvas.drawRightString(PAGE_WIDTH - 22 * mm, PAGE_HEIGHT - 10 * mm, self.document_name)
        canvas.line(22 * mm, 12 * mm, PAGE_WIDTH - 22 * mm, 12 * mm)
        canvas.drawString(22 * mm, 8 * mm, "Idle Slayer community mod documentation")
        canvas.drawRightString(PAGE_WIDTH - 22 * mm, 8 * mm, f"Page {doc.page}")
        canvas.restoreState()


def cover(title: str, subtitle: str):
    flow = []
    banner = ROOT / "Assets" / "banner.png"
    if banner.exists():
        img = Image(str(banner), width=165 * mm, height=47.2 * mm)
        flow.extend([img, Spacer(1, 21 * mm)])
    else:
        flow.append(Spacer(1, 50 * mm))
    flow.extend(
        [
            Paragraph("AutoClimber", STYLES["cover_title"]),
            Paragraph(title, STYLES["cover_subtitle"]),
            Spacer(1, 8 * mm),
            Table([[""]], colWidths=[45 * mm], rowHeights=[1.2 * mm], style=[("BACKGROUND", (0, 0), (-1, -1), CYAN)]),
            Spacer(1, 8 * mm),
            Paragraph(subtitle, STYLES["cover_meta"]),
            Paragraph("Version 1.0.1  |  Tashi", STYLES["cover_meta"]),
            PageBreak(),
        ]
    )
    return flow


def build_user_guide():
    target = OUTPUT / "AutoClimber-User-Guide-1.0.1.pdf"
    story = cover(
        "User Guide",
        "Installation, quick setup, modes, controls, and everyday use",
    )
    story.extend(markdown_to_flowables(ROOT / "USER_GUIDE.md", omit_title=True))
    DocumentationTemplate(target, "User Guide").build(story)
    return target


def build_complete_manual():
    target = OUTPUT / "AutoClimber-Complete-Manual-1.0.1.pdf"
    story = cover(
        "Complete Manual",
        "Routes, platforms, modes, enemies, configuration, logging, and troubleshooting",
    )
    story.extend(markdown_to_flowables(ROOT / "MANUAL.md", omit_title=True))
    chapters = sorted((ROOT / "docs").glob("[0-9][0-9]-*.md"))
    for chapter in chapters:
        story.append(PageBreak())
        story.extend(markdown_to_flowables(chapter))
    DocumentationTemplate(target, "Complete Manual").build(story)
    return target


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    outputs = [build_user_guide(), build_complete_manual()]
    for output in outputs:
        print(output)


if __name__ == "__main__":
    main()
