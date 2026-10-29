// Local-only contract fixture. Explicit file input is controlled by the regression runner.
// Do not deploy: fs is intentional and the fixture is not an application data source.
const http = require('http'), fs = require('fs');
const file = process.env.CANOPY_INTAKE_FIXTURE;
if (!file) throw new Error('Supply an isolated CANOPY_INTAKE_FIXTURE file.');
http.createServer((req, res) => {
 const url = new URL(req.url, 'http://localhost'), fixture = JSON.parse(fs.readFileSync(file, 'utf8'));
 const send = (code, value) => { res.writeHead(code, {'Content-Type':'application/json'}); res.end(JSON.stringify(value)); };
 if (req.method !== 'GET') return send(501,{message:'This integration is read-only.'});
 if (fixture.mode === 'hanging') return setTimeout(() => send(503,{message:'Delayed provider'}), 4000);
 if (fixture.mode === 'html') {res.writeHead(502,{'Content-Type':'text/html'});return res.end('<h1>Bad gateway</h1>');}
 if (fixture.mode === 'oversized') return send(200,[{body:'x'.repeat(20000)}]);
 if (fixture.mode === 'slow') return setTimeout(() => send(200,fixture.issues),400);
 if (fixture.mode === 'outage') return send(503,{message:'Provider unavailable'});
 if (fixture.mode === 'forbidden') return send(403,{message:'Rate limited or forbidden'});
 const match = url.pathname.match(/^\/repos\/([^/]+)\/([^/]+)\/issues(?:\/(\d+))?$/);
 if (!match) return send(501,{message:'Endpoint outside consumed contract'});
 if (`${match[1]}/${match[2]}` !== fixture.repository) return send(404,{message:'Unknown repository'});
 if (req.headers.authorization !== 'Bearer isolated-intake-token' || req.headers['x-github-api-version'] !== fixture.apiVersion) return send(401,{message:'Incorrect configured credential/version'});
 if (match[3]) {const issue=fixture.issues.find(x => x.number===Number(match[3])); return issue ? send(200,issue) : send(404,{message:'Unknown issue'});}
 if (url.searchParams.get('state')!=='all' || url.searchParams.get('sort')!=='created' || url.searchParams.get('direction')!=='asc' || url.searchParams.get('per_page')!=='100') return send(422,{message:'Contract query mismatch'});
 const labels=(url.searchParams.get('labels')||'').split(',').filter(Boolean), page=Number(url.searchParams.get('page'));
 let rows=fixture.issues.filter(x => labels.every(label=>x.labels.some(l=>l.name.toLowerCase()===label.toLowerCase())));
 const size=fixture.pageSize||100;
 const count=fixture.mode==='duplicate' ? rows.slice(0,size) : rows.slice((page-1)*size,page*size);
 if (fixture.mode==='malformed') return send(200,{items:rows});
 if (fixture.mode==='invalidIssue') return send(200,[{id:1}]);
 if (page*size<rows.length || fixture.mode==='truncated' || fixture.mode==='duplicate') res.setHeader('Link',`<http://localhost${url.pathname}?page=${page+1}>; rel="next"`);
 if (fixture.mode==='slowPages') return setTimeout(()=>send(200,count),600);
 return send(200,count);
}).listen(process.env.PORT);
