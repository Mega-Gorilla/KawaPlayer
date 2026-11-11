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
    parts.append(text(62, 407, "読み取りだけ。非公開リストを含めるかは D7 で決める。自動ログイン・編集・自動更新は対象外。", 20, INK, 700))
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
        box(40, 430, 300, 172, "公開リストの選択先", ["事前ベイク URL の番号で取得", "番号は使い回し、ID で照合", "公開データの 200 JSON だけ"], "#EFF8F4", GREEN),
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
        box(390, 120, 320, 170, "2a  VHub 側  #317", ["連携 URL 発行・失効 UI", "個人 API・番号は #145 と共用", "制限・キャッシュ・運用"]),
        box(390, 355, 320, 170, "2b  KawaPlayer 側  #302", ["任意モジュールと個人 UI", "選んだリストだけ共有", "既存処理と権限を再利用"]),
        box(835, 230, 325, 180, "3  統合 QA と公開  #300", ["2 アカウント / PC・Quest", "従来の URL 入力の回帰", "ガイド・運用手順・停止手段"], "#EFF8F4", GREEN),
        arrow("295,315 335,315 335,205 375,205"),
        arrow("335,315 335,440 375,440"),
        text(297, 102, "Gate A：PoC 成功後だけ進む", 18, BLUE, 700),
        text(40, 448, ["D7（非公開の扱い）は", "PoC の前に決める"], 17, RED, 700),
        arrow("720,205 773,205 773,315 820,315", GREEN),
        arrow("720,440 773,440 773,315", GREEN),
        '<rect x="40" y="565" width="1120" height="72" rx="12" fill="#FFF5E8"/>',
        text(60, 596, "PoC が通らなければ停止。既存の VRChat用URL の手動入力は維持する。", 20, INK, 700),
        text(60, 622, "対象リリース、具体的な期限・上限・ベイク数は未定。図の箱が作成済み / 実装済みという意味ではありません。", 16, MUTED),
    ]
    figure("roadmap.svg", 665, "両リポジトリの分担と、実装する順序", "計画案 — PoC → 共通契約 → サーバーとクライアント → 統合 QA。製品コードと本番設定はまだ変更しない", "".join(parts))


GREY = "#F4F6F8"
AMBER = "#FFF5E8"


def note(y, lines, tint=GREY, height=None):
    lines = [lines] if isinstance(lines, str) else lines
    h = height or 26 + 30 * len(lines)
    return f'<rect x="40" y="{y}" width="1120" height="{h}" rx="12" fill="{tint}"/>' + text(62, y + 34, lines, 18, INK)


def requests():
    """The two GETs, who sends them, and what goes out to everyone."""
    parts = [
        text(40, 132, "ブラウザ（PC・スマホ）", 22, BLUE, 700),
        text(440, 132, "各自の KawaPlayer（VRChat）", 22, BLUE, 700),
        text(840, 132, "VHub（サーバー）", 22, BLUE, 700),
        # 1: issue the key
        box(40, 155, 300, 120, "① 連携 URL を発行", ["VChat にログインした画面", "期限つきの閲覧用の鍵"]),
        box(840, 155, 320, 120, "鍵を作って記録", ["ハッシュだけを保存", "寿命と失効を毎回判定"]),
        arrow("350,215 825,215"),
        text(560, 205, "発行", 17, BLUE, 700),
        # 2: the catalogue
        box(440, 305, 300, 130, "② 自分の一覧を読む", ["貼った連携 URL で取得", "GET /r/library/{鍵}"]),
        box(840, 305, 320, 130, "本人の一覧（概要だけ）", ["載せたリストに番号を付ける", "200 JSON・保存させない"]),
        arrow("750,345 825,345"),
        arrow("825,395 750,395"),
        text(760, 385, "一覧と番号", 15, BLUE, 700),
        # 3: the chosen playlist
        box(440, 465, 300, 130, "③ 選んだ 1 件を読む", ["焼き込み済みの番号の URL", "GET /vrcurl/resolve/{番号}"], "#EFF8F4", GREEN),
        box(840, 465, 320, 130, "曲の一覧（公開分）", ["今の公開の状態を確かめる", "今の resolve と同じ JSON"], "#EFF8F4", GREEN),
        arrow("750,505 825,505", GREEN),
        arrow("825,555 750,555", GREEN),
        text(765, 545, "曲と id", 15, GREEN, 700),
        # 4: share
        box(440, 625, 720, 120, "④ 照合して全員へ共有", ["応答の id が選んだリストと同じときだけ、既存の権限と入替確認を通す", "全員へ同期するのは 番号の URL と 識別 default/{id}。鍵は含めない"], "#EFF8F4", GREEN),
        arrow("590,605 590,617", GREEN),
        arrow("190,285 190,370 425,370"),
        text(200, 355, "利用者が URL を貼る", 16, BLUE, 700),
        note(775, ["経路を /r/ と /vrcurl/ の下に置くのは、VRChat からの取得が今の構成でそこだけ届くため（/api/ の下は届かない）。",
                   "③ の番号は検索（#145）と共用する。経路の名前は案で、PoC のあとに確定する。"]),
    ]
    figure("requests.svg", 880, "2 つの GET と、全員へ届くもの", "計画案 — 鍵を使うのは ② だけ。③ と ④ には鍵が無いので、後から入った人も同じ枠で再生できる", "".join(parts))


def numbers():
    """Why the plan reuses the search plan's numbers and checks the id, instead of numbering playlists for good."""
    parts = [
        box(40, 155, 470, 395, "採らない：リストごとの永久の番号", [
            "番号 1 つにリスト 1 つを永久に割り当てる",
            "焼き込んだ数よりリストが増えると、",
            "新しいリストが読めない（作り直しが要る）",
            "廃止した PlaylistViewer 用のプールと",
            "同じ作り。検索の計画でも採らなかった",
        ], "#FFF1F1", RED),
        text(560, 132, "採る：使い回す番号 ＋ id の照合", 22, GREEN, 700),
        box(560, 155, 600, 125, "1. 選ぶ", ["番号 12 → リスト A を読む", "枠に残す：番号の URL …/resolve/12 と 識別 default/A"], "#EFF8F4", GREEN),
        box(560, 310, 600, 95, "2. しばらくして", ["番号 12 が、別の人の操作でリスト B に使い回される"], GREY, MUTED),
        box(560, 435, 600, 125, "3. 枠を更新する", ["…/resolve/12 の応答の id は B。識別 A と違う", "→ 枠は書き換えない（A のまま）。一覧の更新を案内"], "#EFF8F4", GREEN),
        arrow("860,284 860,302", GREEN),
        arrow("860,409 860,427", GREEN),
        note(590, ["番号の URL は取り出しにだけ使い、どのリストかは応答の id で見分ける。選んだときも、あとの更新でも同じ照合を通す。",
                   "プール（4,096 個）、焼き込む配列、照合の手順は、検索の計画（#145）と 1 つにする。"]),
    ]
    figure("numbers.svg", 695, "番号は使い回し、id で照合する", "計画案（D4） — 永久の番号を作らず、検索と同じ resolve プールを共用する", "".join(parts))


def scope():
    """D7: how many people a public-only first version leaves with an empty list, and the three options."""
    def bar(y, label, left, left_text, right_text):
        w = 1120 - 260
        lw = int(w * left)
        return (
            text(40, y + 30, label, 19, INK, 700)
            + f'<rect x="260" y="{y + 6}" width="{lw}" height="38" rx="6" fill="#D7ECE3"/>'
            + f'<rect x="{260 + lw}" y="{y + 6}" width="{w - lw}" height="38" rx="6" fill="#F6DADB"/>'
            + text(276, y + 32, left_text, 17, GREEN, 700)
            + text(276 + lw, y + 32, right_text, 17, RED, 700)
        )

    parts = [
        text(40, 128, "本番の割合（2026-11-11、おおよそ）", 19, MUTED, 700),
        bar(140, "プレイリスト", 0.53, "公開　約半分", "非公開（URL 限定）　約半分"),
        bar(200, "作成した人", 0.55, "公開のリストがある　約半分", "非公開だけ　約半分 → 案 A では一覧が空"),
        box(40, 290, 355, 270, "A. 公開だけ", ["一覧：公開のリストだけ", "＋ 今の計画のまま", "＋ 作りがいちばん小さい", "－ 作成者の約半分は", "　 一覧が空になる"]),
        box(422, 290, 356, 270, "B. 非公開も選べる", ["一覧：非公開も出して選べる", "＋ 全員の役に立つ", "－ 鍵のない取得先が無く、", "　 共有後の更新と後入りを", "　 別に決める必要がある"], "#EFF8F4", GREEN),
        box(805, 290, 355, 270, "C. 非公開は名前だけ", ["一覧：非公開は名前だけ", "＋ 作りが小さい", "－ 一覧に出ても選べない", "　（今までどおり", "　  VRChat用URL を貼る）"]),
        note(590, ["お気に入りを使っている人はごく少ないので、この機能の中心は「作成したプレイリスト」になる。",
                   "どれにするかで通信の作り（6.1）と共有の境界（4 章）が変わる。PoC（Gate A）の前に決める。"], AMBER),
    ]
    figure("scope.svg", 700, "非公開リストをどう扱うか（D7）", "判断待ち — 公開だけにすると、作成者のほぼ半分はワールドで一覧が空になる", "".join(parts))


def key_lifetime():
    """D1: the key is short-lived, while request paths stay in access logs much longer."""
    parts = [
        text(40, 140, "発行", 18, BLUE, 700),
        f'<rect x="40" y="155" width="230" height="40" rx="6" fill="#DCE9F7"/>',
        text(56, 182, "鍵が使える（案：60 分）", 17, BLUE, 700),
        text(280, 182, "← 失効", 18, BLUE, 700),
        f'<rect x="40" y="215" width="1120" height="40" rx="6" fill="#ECEFF2"/>',
        text(56, 242, "経路の記録が残る（途中のプロキシのアクセスログ。数十日）", 17, MUTED, 700),
        text(40, 300, "失効したあとの鍵は、記録に残っていても使えない。寿命を延ばすほど、記録の中の鍵が使える時間も延びる。", 17, INK),
        box(40, 340, 545, 175, "受け入れる", ["寿命を短いまま保つ（延ばさない）", "記録の設定は変えない", "→ D1 の寿命の上限になる"]),
        box(615, 340, 545, 175, "外す", ["一覧の経路だけ、アクセスログから外す", "サーバー側の作業（VHub #317）", "→ 寿命を延ばす余地ができる"]),
        note(545, ["どちらでも、アプリのログには鍵を出さない。VRChat や CDN の記録は制御できないので、残るリスクとして説明する。",
                   "失効で止まるのは後の取得だけ。取得済みの一覧や、共有済みのリストは消えない。"]),
    ]
    figure("key-lifetime.svg", 650, "連携キーの寿命と、経路の記録", "計画案（D1） — 鍵は経路に入るので、短い寿命で記録の影響を抑える", "".join(parts))


if __name__ == "__main__":
    journey()
    scope()
    boundaries()
    key_lifetime()
    requests()
    numbers()
    roadmap()
    print("Generated journey.svg, scope.svg, boundaries.svg, key-lifetime.svg, requests.svg, numbers.svg, roadmap.svg")
