"""Stage 0 of the VHub search (KawaPlayer #145): a stand-in for GET /r/search on this PC only.

It writes every request as it arrived (the request line's bytes, before anything is decoded) and answers with
60 made-up results, so that a VRChat test client shows how a typed search word reaches a server and how long
Udon takes to read the answer. Listens on 127.0.0.1 only.

search_server.py <port> <log file>
"""
import json
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

PORT = int(sys.argv[1])
LOG = sys.argv[2]
ARROW = "→"


def unescape_once(raw: bytes) -> bytes:
    """Undoes %XX once; a % not followed by two hex digits stays, and + stays + (the plan's rule)."""
    out = bytearray()
    i = 0
    while i < len(raw):
        c = raw[i]
        if c == 0x25 and i + 3 <= len(raw):
            try:
                out.append(int(raw[i + 1:i + 3].decode("ascii"), 16))
                i += 3
                continue
            except ValueError:
                pass
        out.append(c)
        i += 1
    return bytes(out)


def extract(target: bytes):
    """The plan's order: after ?q= -> unescape once -> after the last arrow -> trim."""
    mark = target.find(b"?q=")
    if mark < 0:
        return None, "no ?q="
    tail = target[mark + 3:]
    try:
        text = unescape_once(tail).decode("utf-8")
    except UnicodeDecodeError:
        return None, "not UTF-8"
    cut = text.rfind(ARROW)
    word = text[cut + 1:] if cut >= 0 else text
    return word.strip(), "ok"


def results(word: str):
    items = []
    for n in range(60):
        item = {
            "id": "sample%017d" % n,
            "name": "サンプルのプレイリスト %02d Sample Playlist" % n,
            "ownerName": "sample_owner_%02d" % (n % 7),
            "trackCount": 5 + n,
            "resolveIndex": 100 + n,
            "updatedAt": "2026-11-%02dT03:00:00.000Z" % (1 + n % 28),
        }
        if n % 3 == 2:
            item["matchedTrack"] = "サンプルの曲名 Sample Track %02d" % n
        items.append(item)
    return {"ok": True, "query": word, "truncated": True, "items": items}


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_GET(self):
        line = self.raw_requestline.rstrip(b"\r\n")
        parts = line.split(b" ")
        target = b" ".join(parts[1:-1]) if len(parts) >= 3 else line
        word, how = extract(target)
        record = {
            "time": time.strftime("%H:%M:%S"),
            "line_ascii": line.decode("latin-1").encode("unicode_escape").decode("ascii"),
            "target_len": len(target),
            "spaces_in_target": target.count(b" "),
            "word": word,
            "extract": how,
            "user_agent": self.headers.get("User-Agent", ""),
            "accept": self.headers.get("Accept", ""),
            "host": self.headers.get("Host", ""),
        }
        with open(LOG, "a", encoding="utf-8") as f:
            f.write(json.dumps(record, ensure_ascii=False) + "\n")
        if target.startswith(b"/r/search"):
            if word is None:
                body = json.dumps({"ok": False, "error": "Invalid query"}).encode("utf-8")
                status = 400
            else:
                body = json.dumps(results(word), ensure_ascii=False).encode("utf-8")
                status = 200
        else:
            body = json.dumps({"ok": False, "error": "Not found"}).encode("utf-8")
            status = 404
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, fmt, *args):
        pass


if __name__ == "__main__":
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    with open(LOG, "a", encoding="utf-8") as f:
        f.write(json.dumps({"time": time.strftime("%H:%M:%S"), "started": PORT}) + "\n")
    server.serve_forever()
