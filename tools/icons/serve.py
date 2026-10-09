"""Serves render.html and saves each icon the page POSTs (a PNG data URL) into the folder given as argv[1]."""
import base64, http.server, os, sys, urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)


class H(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k):
        super().__init__(*a, directory=HERE, **k)

    def do_POST(self):
        q = urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)
        name = ''.join(ch for ch in q.get('name', ['x'])[0] if ch.isalnum())
        body = self.rfile.read(int(self.headers['Content-Length'])).decode()
        data = base64.b64decode(body.split(',', 1)[1])
        with open(os.path.join(OUT, name + '.png'), 'wb') as f:
            f.write(data)
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b'ok')


http.server.ThreadingHTTPServer(('127.0.0.1', 8765), H).serve_forever()
