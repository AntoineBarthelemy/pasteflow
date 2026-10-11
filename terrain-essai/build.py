"""Builds the test bench page (index.html) from page.html, scenarios.json and bon-de-commande.png.

Run it after changing a scenario: python terrain-essai/build.py
"""
import base64
import json
import pathlib

here = pathlib.Path(__file__).parent
page = (here / "page.html").read_text(encoding="utf-8")
scenarios = json.loads((here / "scenarios.json").read_text(encoding="utf-8"))
data = json.dumps(scenarios, ensure_ascii=False).replace("</", "<\\/")
scan = "data:image/png;base64," + base64.b64encode((here / "bon-de-commande.png").read_bytes()).decode("ascii")

html = page.replace("/*SCENARIOS*/", data).replace("__SCAN__", scan)
(here / "index.html").write_text("<!doctype html>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n" + html, encoding="utf-8")
print("index.html construit")
