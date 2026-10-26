// Focused first-use workspace contract from GET /api/workspace. Test-only:
// writes are intentionally unsupported; real MCP mutation checks use DuckDB.
const http = require('http');
http.createServer((req,res) => {
 if (req.method === 'GET' && new URL(req.url,'http://localhost').pathname === '/api/workspace') {
  res.writeHead(200,{'Content-Type':'application/json'});
  res.end(JSON.stringify({projects:[],items:[],statuses:[],dependencies:[],people:[]}));
 } else { res.writeHead(501,{'Content-Type':'application/json'});res.end(JSON.stringify({title:'This test mock models only workspace reads.'})); }
}).listen(process.env.PORT);
