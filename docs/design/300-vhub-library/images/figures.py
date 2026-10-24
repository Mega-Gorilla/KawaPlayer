"""Draw the planning figures for issue #300 using only Python's standard library.

Run: python docs/design/300-vhub-library/images/figures.py
The SVGs are source-controlled diagrams, not screenshots of an implemented UI.
"""

from html import escape
from pathlib import Path


OUT = Path(__file__).resolve().parent
INK = "#183047"
MUTED = "#465D70"
BLUE = "#195DAA"
GREEN = "#176C55"
RED = "#A3343A"


def text(x, y, lines, size=18, color=INK, weight=400):
    if isinstance(lines, str):
        lines = [lines]
    spans = "".join(
        f'<tspan x="{x}" dy="{0 if i == 0 else size + 10}">{escape(line)}</tspan>'
        for i, line in enumerate(lines)
    )
    return f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{weight}" fill="{color}">{spans}</text>'


def box(x, y, w, h, title, lines, tint="#EFF5FC", stroke=BLUE):
    return (
        f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="14" fill="{tint}" stroke="{stroke}" stroke-width="2"/>'
        + text(x + 20, y + 36, title, 21, stroke, 700)
        + text(x + 20, y + 77, lines, 18)
    )


def arrow(points, color=BLUE):
    return f'<polyline points="{points}" fill="none" stroke="{color}" stroke-width="3" marker-end="url(#{color[1:]})"/>'


def figure(name, height, title, subtitle, contents):
    markers = "".join(
        f'<marker id="{c[1:]}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse"><path d="M 0 0 L 10 5 L 0 10 z" fill="{c}"/></marker>'
        for c in (BLUE, GREEN, RED)
    )
    svg = (
        f'<svg xmlns="http://www.w3.org/2000/svg" width="1200" height="{height}" viewBox="0 0 1200 {height}" role="img" aria-labelledby="title desc">'
        f'<title id="title">{escape(title)}</title><desc id="desc">{escape(subtitle)}</desc>'
        '<style>text{font-family:"Yu Gothic","Meiryo","Noto Sans CJK JP",sans-serif}</style>'
        f'<defs>{markers}</defs><rect width="1200" height="{height}" fill="#FFFFFF"/>'
        + text(40, 48, title, 28, INK, 700)
        + text(40, 82, subtitle, 17, MUTED)
        + contents
        + "</svg>\n"
    )
    (OUT / name).write_text(svg, encoding="utf-8", newline="\n")


def journey():
    parts = []
    cards = [
        (40, "1  ブラウザで認証", ["VChat でログイン", "パスワードは", "ワールドに渡さない"]),
        (335, "2  連携 URL を入力", ["閲覧専用・期限あり", "URL を専用欄へ貼る"]),
        (630, "3  自分の一覧を開く", ["自作 / お気に入り", "全員へ同期しない"]),
        (925, "4  選んだ 1 件を共有", ["既存の権限と入替確認", "選んだリストだけ追加"]),
    ]
    for i, (x, title, lines) in enumerate(cards):
        parts.append(box(x, 145, 235, 185, title, lines, "#EFF8F4" if i == 3 else "#EFF5FC", GREEN if i == 3 else BLUE))
        if i < 3:
            parts.append(arrow(f"{x + 244},238 {x + 280},238", GREEN if i == 2 else BLUE))
    parts.append('<rect x="40" y="373" width="1120" height="100" rx="12" fill="#F4F6F8"/>')
    parts.append(text(62, 407, "最初は公開プレイリストの読み取りだけ。自動ログイン・編集・自動更新は対象外。", 20, INK, 700))
    parts.append(text(62, 443, "これは利用の流れの設計図です。新機能の画面や実機での動作確認が済んだことを示す画像ではありません。", 17, MUTED))
    figure("journey.svg", 510, "使う人から見た VHub Library 連携", "計画案 / issue #300 — Web で認証し、VRChat では選んで共有する", "".join(parts))


def boundaries():
    parts = [
        text(40, 132, "VHub Playlist", 23, BLUE, 700),
        text(440, 132, "各自の KawaPlayer モジュール", 23, BLUE, 700),
        text(840, 132, "全員で使う共有状態", 23, GREEN, 700),
        box(40, 170, 300, 172, "個人カタログ API", ["Web で本人を確認して発行", "読み取り専用 grant を検証", "自作 / お気に入りの概要だけ"]),
        box(440, 170, 300, 172, "ローカルのライブラリ", ["連携 URL はここでだけ使う", "接続・一覧表示・切断", "所有権や共有枠を変えない"]),
        box(840, 170, 320, 172, "共有してはいけないもの", ["連携キー / Cookie / token", "個人の一覧・接続状態", "PlayerData にも保存しない"], "#FFF1F1", RED),
        arrow("350,250 425,250"),
        text(352, 222, "200 JSON", 16, BLUE, 700),
        '<line x1="790" y1="150" x2="790" y2="620" stroke="#A3343A" stroke-width="2" stroke-dasharray="7 7"/>',
        text(748, 381, "同期境界", 17, RED, 700),
        arrow("590,350 590,421", GREEN),
        text(610, 386, "選択", 18, GREEN, 700),
        box(40, 430, 300, 172, "公開リストの選択先", ["事前ベイク URL の番号で取得", "番号と ID は固定する案", "公開データの 200 JSON だけ"], "#EFF8F4", GREEN),
        box(440, 430, 300, 172, "検証して既存処理へ", ["応答 ID と選択 ID が一致", "権限・入替確認を通す", "個人キーを含まない参照"], "#EFF8F4", GREEN),
        box(840, 430, 320, 172, "選んだプレイリスト", ["曲名・動画 Pool の番号 / URL", "キーなしの更新用参照", "後入りの参加者も再生"], "#EFF8F4", GREEN),
        arrow("350,516 425,516", GREEN),
        arrow("750,516 825,516", GREEN),
        text(40, 656, "注意：ローカル表示は、ワールド制作者等からの秘匿を保証するものではありません。", 18, RED, 700),
        text(40, 687, "連携 URL は所持者が使える鍵です。短命・範囲限定・失効可能にし、取得済みの情報まで消せるとは説明しません。", 17, MUTED),
    ]
    figure("boundaries.svg", 720, "個人用の鍵と、全員へ共有するデータを分ける", "設計の必須条件 — 既存 DynamicPlaylist の取得元 URL は同期されるため、そのまま個人 URL を渡さない", "".join(parts))


def roadmap():
    parts = [
        box(40, 230, 245, 180, "1  PoC と契約  #301", ["PC / Quest の取得・容量", "鍵・ログ・同期の漏れ", "通った契約を文書化"]),
        box(390, 120, 320, 170, "2a  VHub 側  #317", ["連携 URL 発行・失効 UI", "個人 API / 公開の選択先", "制限・キャッシュ・運用"]),
        box(390, 355, 320, 170, "2b  KawaPlayer 側  #302", ["任意モジュールと個人 UI", "選んだリストだけ共有", "既存処理と権限を再利用"]),
        box(835, 230, 325, 180, "3  統合 QA と公開  #300", ["2 アカウント / PC・Quest", "従来の URL 入力の回帰", "ガイド・運用手順・停止手段"], "#EFF8F4", GREEN),
        arrow("295,315 335,315 335,205 375,205"),
        arrow("335,315 335,440 375,440"),
        text(297, 102, "Gate A：PoC 成功後だけ進む", 18, BLUE, 700),
        arrow("720,205 773,205 773,315 820,315", GREEN),
        arrow("720,440 773,440 773,315", GREEN),
        '<rect x="40" y="565" width="1120" height="72" rx="12" fill="#FFF5E8"/>',
        text(60, 596, "PoC が通らなければ停止。既存の VRChat用URL の手動入力は維持する。", 20, INK, 700),
        text(60, 622, "対象リリース、具体的な期限・上限・ベイク数は未定。図の箱が作成済み / 実装済みという意味ではありません。", 16, MUTED),
    ]
    figure("roadmap.svg", 665, "両リポジトリの分担と、実装する順序", "計画案 — PoC → 共通契約 → サーバーとクライアント → 統合 QA。製品コードと本番設定はまだ変更しない", "".join(parts))


if __name__ == "__main__":
    journey()
    boundaries()
    roadmap()
    print("Generated journey.svg, boundaries.svg, roadmap.svg")
