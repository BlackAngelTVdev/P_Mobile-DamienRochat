#!/bin/bash
 
# Config
SINCE="2026-01-13"
OUTPUT_DIR="Doc"
OUTPUT_FILE="$OUTPUT_DIR/journal_de_travail.csv"
OUTPUT_XLSX="$OUTPUT_DIR/journal_de_travail.xlsx"
 
mkdir -p "$OUTPUT_DIR"
 
echo "🚀 Extraction propre en cours..."
 
# 1. On écrit l'en-tête
echo "Date,Nom,Temps,État,Description" > "$OUTPUT_FILE"
 
# 2. Git log avec séparateurs spéciaux
git log --all --since="$SINCE" --reverse --date=format:'%d/%m/%Y' --pretty=format:%ad%x1f%s%x1f%b%x1e \
| awk -v RS='\036' -F '\037' '
NF {
    current_date = $1;
    nom_commit = $2;
    body = $3;

    # Le séparateur de records peut laisser un saut de ligne en tête
    gsub(/\r|\n/, "", current_date);
    gsub(/^ +| +$/, "", current_date);
 
    # Nettoyage des sauts de ligne pour éviter de casser le CSV
    gsub(/\r|\n/, " ", nom_commit);
    gsub(/\r|\n/, " ", body);
   
    full_text = nom_commit " " body;
 
    # Initialisation
    temps = "[?]";
    etat = "[?]";
 
    # Extraction Temps [1h45min]
    if (match(full_text, /\[[0-9hmin]+\]/)) {
        temps = substr(full_text, RSTART, RLENGTH);
        sub(/\[[0-9hmin]+\]/, "", full_text);
    }
   
    # Extraction État [DONE]
    if (match(full_text, /\[[A-Z]+\]/)) {
        etat = substr(full_text, RSTART, RLENGTH);
        sub(/\[[A-Z]+\]/, "", full_text);
    }
 
    # NETTOYAGE FINAL : on double les guillemets pour le format CSV
    gsub(/"/, "\"\"", nom_commit);
    gsub(/"/, "\"\"", full_text);
    # On enlève les espaces en trop au début et à la fin
    gsub(/^ +| +$/, "", nom_commit);
    gsub(/^ +| +$/, "", full_text);
 
    # SÉPARATION DES JOURS
    if (last_date != "" && last_date != current_date) {
        print "" >> "'$OUTPUT_FILE'";
    }
    last_date = current_date;
 
    # ÉCRITURE DE LA LIGNE (Une seule fois, bien proprement)
    printf "\"%s\",\"%s\",\"%s\",\"%s\",\"%s\"\n", current_date, nom_commit, temps, etat, full_text >> "'$OUTPUT_FILE'";
}
'
 
echo "📄 CSV généré : $OUTPUT_FILE"

# 3. Conversion automatique CSV -> XLSX
if command -v python3 >/dev/null 2>&1; then
    PYTHON_BIN="python3"
elif command -v python >/dev/null 2>&1; then
    PYTHON_BIN="python"
else
    PYTHON_BIN=""
fi

if [ -n "$PYTHON_BIN" ]; then
    PY_OUTPUT=$(OUTPUT_FILE="$OUTPUT_FILE" OUTPUT_XLSX="$OUTPUT_XLSX" "$PYTHON_BIN" - << 'PYEOF' 2>&1
import csv
import os
import re
import sys
from collections import defaultdict
from datetime import datetime

src = os.environ["OUTPUT_FILE"]
dst = os.environ["OUTPUT_XLSX"]

try:
    from openpyxl import Workbook
    from openpyxl.chart import BarChart, LineChart, Reference
    from openpyxl.styles import Alignment, Font, PatternFill
except Exception:
    sys.exit(2)


def parse_minutes(raw_value):
    if not raw_value:
        return 0

    value = str(raw_value).strip().strip("[]").lower()
    if value == "?":
        return 0

    compact = value.replace(" ", "")

    # 1h23, 1h23min, 1h -> heures + minutes optionnelles
    match_hm = re.fullmatch(r"(\d+)h(?:(\d+)(?:min)?)?", compact)
    if match_hm:
        hours = int(match_hm.group(1))
        minutes = int(match_hm.group(2) or 0)
        return hours * 60 + minutes

    # 34min -> minutes explicites
    match_min = re.fullmatch(r"(\d+)min", compact)
    if match_min:
        return int(match_min.group(1))

    # 90 -> on considere que ce sont des minutes
    if compact.isdigit():
        return int(compact)

    return 0


def to_hhmm(total_minutes):
    total = int(total_minutes or 0)
    hours, minutes = divmod(total, 60)
    return f"{hours}h{minutes:02d}"


wb = Workbook()
ws = wb.active
ws.title = "Journal"

rows = []

with open(src, newline="", encoding="utf-8") as f:
    for row in csv.reader(f):
        if not row or all(str(c).strip() == "" for c in row):
            continue
        rows.append(row)

header_fill = PatternFill(start_color="1F4E78", end_color="1F4E78", fill_type="solid")
day_fill = PatternFill(start_color="2E75B6", end_color="2E75B6", fill_type="solid")
row_fill = PatternFill(start_color="EEF5FC", end_color="EEF5FC", fill_type="solid")

if rows:
    ws.append(rows[0])
else:
    ws.append(["Date", "Nom", "Temps", "Etat", "Description"])

for cell in ws[1]:
    cell.font = Font(bold=True, color="FFFFFF")
    cell.fill = header_fill
    cell.alignment = Alignment(horizontal="center")

grouped_rows = defaultdict(list)
day_order = []

for row in rows[1:]:
    if len(row) < 5:
        continue

    date_label = str(row[0]).strip()
    if not date_label:
        continue

    if date_label not in grouped_rows:
        day_order.append(date_label)

    grouped_rows[date_label].append(row)

for day in day_order:
    ws.append([day, "", "", "", ""])
    day_row_idx = ws.max_row
    ws.merge_cells(start_row=day_row_idx, start_column=1, end_row=day_row_idx, end_column=5)
    day_cell = ws.cell(row=day_row_idx, column=1)
    day_cell.font = Font(bold=True, color="FFFFFF")
    day_cell.fill = day_fill
    day_cell.alignment = Alignment(horizontal="left")

    day_total_minutes = 0

    for entry in grouped_rows[day]:
        ws.append(["", entry[1], entry[2], entry[3], entry[4]])
        data_row_idx = ws.max_row
        for col_idx in range(1, 6):
            ws.cell(row=data_row_idx, column=col_idx).fill = row_fill
        day_total_minutes += parse_minutes(entry[2])

    ws.append(["", "Total du jour", f"{to_hhmm(day_total_minutes)} ({day_total_minutes} min)", "", ""])
    total_row_idx = ws.max_row
    total_fill = PatternFill(start_color="D6E8FA", end_color="D6E8FA", fill_type="solid")
    for col_idx in range(1, 6):
        ws.cell(row=total_row_idx, column=col_idx).fill = total_fill
    ws.cell(row=total_row_idx, column=2).font = Font(bold=True)
    ws.cell(row=total_row_idx, column=3).font = Font(bold=True)

    # Ligne vide entre les jours
    ws.append(["", "", "", "", ""])

ws.freeze_panes = "A2"
ws.auto_filter.ref = f"A1:E{ws.max_row}"
ws.column_dimensions["A"].width = 14
ws.column_dimensions["B"].width = 45
ws.column_dimensions["C"].width = 14
ws.column_dimensions["D"].width = 12
ws.column_dimensions["E"].width = 90

daily = defaultdict(lambda: {"minutes": 0})

for row in rows[1:]:
    if len(row) < 3:
        continue

    date_label = str(row[0]).strip()
    if not date_label:
        continue

    minutes = parse_minutes(row[2])
    daily[date_label]["minutes"] += minutes

ws2 = wb.create_sheet("Tableau de bord")
ws2.append(["Date", "Total (min)", "Total (h)", "Total (h:min)"])

sorted_days = sorted(
    daily.items(),
    key=lambda item: datetime.strptime(item[0], "%d/%m/%Y")
)

for day, stats in sorted_days:
    ws2.append([
        day,
        stats["minutes"],
        round(stats["minutes"] / 60, 2),
        to_hhmm(stats["minutes"]),
    ])

for col in ws2.columns:
    max_len = max((len(str(cell.value)) if cell.value is not None else 0) for cell in col)
    ws2.column_dimensions[col[0].column_letter].width = min(max_len + 2, 30)

if sorted_days:
    total_minutes = sum(stats["minutes"] for _, stats in sorted_days)
    nb_days = len(sorted_days)
    top_day, top_stats = max(sorted_days, key=lambda item: item[1]["minutes"])

    for cell in ws2[1]:
        cell.font = Font(bold=True, color="FFFFFF")
        cell.fill = header_fill
        cell.alignment = Alignment(horizontal="center")

    ws2.freeze_panes = "A2"
    ws2.auto_filter.ref = f"A1:D{ws2.max_row}"

    ws2["F1"] = "Indicateur"
    ws2["G1"] = "Valeur"
    ws2["F2"] = "Jours suivis"
    ws2["G2"] = nb_days
    ws2["F3"] = "Temps total"
    ws2["G3"] = to_hhmm(total_minutes)
    ws2["F4"] = "Moyenne / jour"
    ws2["G4"] = to_hhmm(round(total_minutes / nb_days))
    ws2["F5"] = "Jour le plus charge"
    ws2["G5"] = f"{top_day} ({to_hhmm(top_stats['minutes'])})"

    for row in ws2["F1:G1"]:
        for cell in row:
            cell.font = Font(bold=True, color="FFFFFF")
            cell.fill = header_fill
            cell.alignment = Alignment(horizontal="center")

    for row_idx in range(2, 6):
        ws2[f"F{row_idx}"].font = Font(bold=True)

    max_row = ws2.max_row
    date_ref = Reference(ws2, min_col=1, min_row=2, max_row=max_row)

    bar = BarChart()
    bar.title = "Charge par jour (heures)"
    bar.y_axis.title = "Heures"
    bar.x_axis.title = "Date"
    bar_data = Reference(ws2, min_col=3, min_row=1, max_row=max_row)
    bar.add_data(bar_data, titles_from_data=True)
    bar.set_categories(date_ref)
    bar.style = 10
    bar.height = 8
    bar.width = 16
    ws2.add_chart(bar, "M8")

    ws2["I1"] = "Date"
    ws2["J1"] = "Cumul (h)"
    ws2["I1"].font = Font(bold=True)
    ws2["J1"].font = Font(bold=True)

    running_minutes = 0
    for idx, (day, stats) in enumerate(sorted_days, start=2):
        running_minutes += stats["minutes"]
        ws2[f"I{idx}"] = day
        ws2[f"J{idx}"] = round(running_minutes / 60, 2)

    line = LineChart()
    line.title = "Cumul des heures"
    line.y_axis.title = "Heures"
    line.x_axis.title = "Date"
    line_data = Reference(ws2, min_col=10, min_row=1, max_row=max_row)
    line.add_data(line_data, titles_from_data=True)
    line.set_categories(date_ref)
    line.smooth = True
    if line.series:
        line.series[0].marker = None
    line.height = 8
    line.width = 16
    ws2.add_chart(line, "M26")

try:
    wb.save(dst)
except PermissionError:
    base, ext = os.path.splitext(dst)
    fallback = f"{base}_{datetime.now().strftime('%Y%m%d_%H%M%S')}{ext}"
    wb.save(fallback)
    print(f"FALLBACK_XLSX={fallback}")
except Exception as exc:
    print(f"SAVE_ERROR={exc}")
    sys.exit(4)
PYEOF
)

    PY_STATUS=$?
    if [ $PY_STATUS -eq 0 ]; then
        if grep -q '^FALLBACK_XLSX=' <<< "$PY_OUTPUT"; then
            FALLBACK_FILE=$(grep '^FALLBACK_XLSX=' <<< "$PY_OUTPUT" | sed 's/^FALLBACK_XLSX=//')
            echo "⚠️ Le fichier principal est verrouillé (probablement ouvert)."
            echo "✅ Excel généré avec un nom alternatif : $FALLBACK_FILE"
        else
            echo "✅ Excel généré automatiquement : $OUTPUT_XLSX"
        fi
    elif [ $PY_STATUS -eq 2 ]; then
        echo "⚠️ Impossible de générer l'Excel automatiquement (module python 'openpyxl' manquant)."
        echo "   Installe-le avec: pip install openpyxl"
    else
        if [ -n "$PY_OUTPUT" ]; then
            echo "$PY_OUTPUT"
        fi
        echo "⚠️ Impossible de générer l'Excel automatiquement (erreur d'ecriture)."
        echo "   Verifie que le dossier est accessible et que le fichier .xlsx n'est pas ouvert."
    fi
else
    echo "⚠️ Python introuvable, conversion Excel ignorée."
fi

echo "✨ C'est fini !"
