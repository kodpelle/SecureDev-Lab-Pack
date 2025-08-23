
    const base = location.origin;
    let jwt = null;

        const el = id => document.getElementById(id);
const setJwtStatus = () => {
    const signed = !!jwt;
    el('jwtStatus').textContent = signed ? 'Signed in' : 'Signed out';
    el('jwtStatus').className = 'pill ' + (signed ? 'success' : '');
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
    const r = await call('POST', '/notes', {
        title: el('title').value,
        content: el('content').value
    }, true);

    if (r.json) {
        const note = r.json;
        el('createSafeOut').textContent =
            `#${note.id} ${note.title}\n${note.content}`;
    } else {
        el('createSafeOut').textContent = JSON.stringify(r, null, 2);
    }
};

// WARNING uses .innerHTML   ---> open to XSS
//<img src=x onerror=alert('XSS!')>

el('btnCreateBug').onclick = async () => {
    const r = await call('POST', '/notes-bug', {
        title: el('title').value,
        content: el('content').value,
        ownerId: el('owner').value
    });

    if (r.json) {
        const note = r.json;
        el('createBugOut').innerHTML =
            `<h3>${note.title}</h3><p>${note.content}</p>`;
    } else {
        el('createBugOut').textContent = JSON.stringify(r, null, 2);
    }
};
        // Get notes by id
        el('btnGetSafe').onclick = async () => {
            const id = el('nid').value;
    const r = await call('GET', '/notes/' + id, undefined, true);
    el('getSafeOut').textContent = JSON.stringify(r, null, 2);
        };
el('btnGetBug').onclick = async () => {
    const id = el('nid').value;
    const r = await call('GET', '/notes-bug/' + id);
    el('getBugOut').textContent = JSON.stringify(r, null, 2);
};

const out = (o) => el('cryptoOut').textContent = JSON.stringify(o, null, 2);

// --- CRYPTOGRAPHY ---
let lastGcm; 
el('btnGcmEnc').onclick = async () => {
    const r = await fetch(base + '/crypto/aes/gcm/encrypt', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ plaintext: el('c_pt').value })
    });
    lastGcm = await r.json();
    out({ status: r.status, gcm: lastGcm });
};
el('btnGcmDec').onclick = async () => {
    if (!lastGcm) return out({ error: 'Encrypt first' });
    const r = await fetch(base + '/crypto/aes/gcm/decrypt', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            base64Key: lastGcm.base64Key,
            base64Nonce: lastGcm.base64Nonce,
            base64Ciphertext: lastGcm.base64Ciphertext,
            base64Tag: lastGcm.base64Tag
        })
    });
    out({ status: r.status, decrypted: await r.json() });
};

// --- AES-CBC BUG ---
el('btnCbcBug').onclick = async () => {
    const r = await fetch(base + '/crypto/aes/cbc-bug/encrypt', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ plaintext: el('c_pt').value })
    });
    out({
        status: r.status, cbcBug: await r.json(),
        note: 'CBC uses fixed IV (all zeros) and has no integrity tag – vulnerable to IV reuse and bit flipping'
    });
};

// --- PBKDF2 vs SHA-256 ---
let lastPbkdf2;
el('btnPbkdf2').onclick = async () => {
    const it = parseInt(el('c_iter').value || '100000', 10);
    const r = await fetch(base + '/crypto/hash/pbkdf2', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password: el('c_pt').value, iterations: it })
    });
    lastPbkdf2 = await r.json();
    out({ status: r.status, pbkdf2: lastPbkdf2 });
};
el('btnVerify').onclick = async () => {
    if (!lastPbkdf2) return out({ error: 'Run PBKDF2 first' });
    const it = parseInt(el('c_iter').value || '100000', 10);
    const r = await fetch(base + '/crypto/hash/verify', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            password: el('c_pt').value,
            hashBase64: lastPbkdf2.hashBase64,
            iterations: it
        })
    });
    out({ status: r.status, verify: await r.json() });
};
el('btnShaBug').onclick = async () => {
    const r = await fetch(base + '/crypto/hash/sha256-bug', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password: el('c_pt').value })
    });
    out({
        status: r.status, sha256Bug: await r.json(),
        warning: 'SHA-256 alone is fast and unsuitable for passwords (no salt/iterations).'
    });
};

const infoBtn = document.getElementById('cryptoInfoBtn');
const infoBox = document.getElementById('cryptoHelp');

if (infoBtn && infoBox) {
    infoBtn.addEventListener('click', () => {
        const isHidden = infoBox.hasAttribute('hidden');
        if (isHidden) infoBox.removeAttribute('hidden'); else infoBox.setAttribute('hidden', '');
        infoBtn.setAttribute('aria-expanded', String(isHidden));
    });
}