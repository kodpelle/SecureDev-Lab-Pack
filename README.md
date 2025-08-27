# BuggyNotes – Secure vs Insecure demo
BuggyNotes is a minimal demo application that shows **secure vs insecure coding practices** side-by-side.  
It is intended **for learning purposes only** in the context of developer security training.  

![BuggyNotes screenshot](./BuggyNotes.Api/imgs//buggynotesdemo.jpg)
1) Authentication

Sign in (secure): verifies password hash.

Sign in (insecure): skips verification (demonstrates broken auth).

Show my profile: calls /me and requires a valid JWT.

![BuggyNotes screenshot](./BuggyNotes.Api/imgs//buggynotesauth.png)

2) Search notes (SQL injection)

Secure: /notes/search-safe uses EF parameterization.

Insecure: /notes/search-bug uses FromSqlRaw with string interpolation.

Try normal query (e.g. Hello).

Then try an injection: % or %' OR 1=1 -- (insecure should leak more rows).

![BuggyNotes screenshot](./BuggyNotes.Api/imgs//buggynotessearch.png)

3) Create note (XSS)

Secure render: server returns JSON; UI writes with textContent.

Insecure render: UI uses innerHTML → try payloads like:

<img src=x onerror=alert('XSS!')>

![BuggyNotes screenshot](./BuggyNotes.Api/imgs/buggynotescreate.png)


4) Open note by id (IDOR / broken object level auth)

Secure: /notes/{id} checks that the note belongs to you.

Insecure: /notes-bug/{id} returns any note, regardless of owner.

![BuggyNotes screenshot](./BuggyNotes.Api/imgs/buggynotesgetbyid.png)

5) Crypto demo

AES-GCM (safe): authenticated encryption (nonce + tag).

AES-CBC (insecure demo): fixed IV, no integrity tag → IV reuse / bit-flipping risk.

PBKDF2 (safe): salted, iterated hashing; shows time cost.

SHA-256 (insecure for passwords): fast, unsalted → weak vs password cracking.

![BuggyNotes screenshot](./BuggyNotes.Api/imgs/buggynotescrypto.png)

Security lessons (cheat-sheet)

Never build SQL with string concatenation. Use parameters / LINQ.

Enforce ownership checks on object access (/notes/{id} should verify OwnerId).

Don’t render untrusted HTML. Use textContent/escape, CSP, and avoid .innerHTML.

Password storage: use slow, salted hash (PBKDF2/bcrypt/Argon2). Never plain SHA-256.

JWT secrets: keep out of source; use user-secrets or environment variables.