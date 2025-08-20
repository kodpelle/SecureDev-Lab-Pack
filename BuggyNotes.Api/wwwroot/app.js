
    const base = location.origin;
    let jwt = null;

        const el = id => document.getElementById(id);
        const setJwtStatus = () => {
        el('jwtStatus').textContent = jwt ? 'JWT: present' : 'JWT: none';
    el('jwtStatus').className = 'pill ' + (jwt ? 'success' : '');
        };
    setJwtStatus();

    async function call(method, path, body, withJwt = false) {
            const headers = {'Content-Type': 'application/json' };
    if (withJwt && jwt) headers['Authorization'] = 'Bearer ' + jwt;

    const res = await fetch(base + path, {
        method,
        headers,
        body: body ? JSON.stringify(body) : undefined
            });


    const demo = res.headers.get('X-Demo-Mode');
    if (demo) {
        el('demoMode').textContent = 'X-Demo-Mode: ' + demo;
    el('demoMode').className = 'pill ' + (demo === 'insecure' ? 'warn' : 'success');
            }

    const text = await res.text();
    let json;
    try {json = JSON.parse(text); } catch {json = text; }
    return {status: res.status, json };
        }

        // Auth
        el('btnRegister').onclick = async () => {
            const r = await call('POST', '/auth/register', {UserName: el('u').value, Password: el('p').value });
    el('authOut').textContent = JSON.stringify(r, null, 2);
        };
        el('btnLogin').onclick = async () => {
            const r = await call('POST', '/auth/login', {UserName: el('u').value, Password: el('p').value });
    el('authOut').textContent = JSON.stringify(r, null, 2);
    jwt = r.json?.token || null;
    setJwtStatus();
        };
        el('btnLoginBug').onclick = async () => {
            const r = await call('POST', '/auth/login-bug', {UserName: el('u').value, Password: 'ignored' });
    el('authOut').textContent = JSON.stringify(r, null, 2);
    jwt = r.json?.token || null; // dangerous on purpose
    setJwtStatus();
        };
        el('btnMe').onclick = async () => {
            const r = await call('GET', '/me', undefined, true);
    el('authOut').textContent = JSON.stringify(r, null, 2);
        };

        // Search
        el('btnSearchSafe').onclick = async () => {
            const r = await fetch(base + '/notes/search-safe?q=' + encodeURIComponent(el('q').value), {
        headers: jwt ? {'Authorization': 'Bearer ' + jwt } : { }
            });
    el('searchSafeOut').textContent = JSON.stringify({status: r.status, json: await r.json() }, null, 2);
        };
el('btnSearchBug').onclick = async () => {
    const r = await fetch(base + '/notes/search-bug?q=' + encodeURIComponent(el('q').value), {
        headers: jwt ? { 'Authorization': 'Bearer ' + jwt } : {}
    });
    el('searchBugOut').textContent = JSON.stringify({ status: r.status, json: await r.json() }, null, 2);
};

        // Create notes
        el('btnCreateSafe').onclick = async () => {
            const r = await call('POST', '/notes', {title: el('title').value, content: el('content').value }, true);
    el('createSafeOut').textContent = JSON.stringify(r, null, 2);
        };
        el('btnCreateBug').onclick = async () => {
            const r = await call('POST', '/notes-bug', {
        title: el('title').value, content: el('content').value, ownerId: el('owner').value
            });
    el('createBugOut').textContent = JSON.stringify(r, null, 2);
        };

        // Get notes by id
        el('btnGetSafe').onclick = async () => {
            const id = el('nid').value;
    const r = await call('GET', '/notes/' + id, undefined, true);
    el('getSafeOut').textContent = JSON.stringify(r, null, 2);
        };
        el('btnGetBug').onclick = async () => {
            const id = el('nid').value;
    const r = await call('GET', '/notes/' + id + '-bug');
    el('getBugOut').textContent = JSON.stringify(r, null, 2);
        };