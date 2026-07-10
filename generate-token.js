const crypto = require('crypto');
function base64url(str) { return Buffer.from(str).toString('base64').replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_'); }
const header = { alg: 'HS256', typ: 'JWT' };
const payload = { sub: '00000000-0000-0000-0000-000000000000', role: 'SuperAdmin', exp: Math.floor(Date.now() / 1000) + 3600, iss: 'LMS.Api', aud: 'LMS.Client' };
const encodedHeader = base64url(JSON.stringify(header));
const encodedPayload = base64url(JSON.stringify(payload));
const signature = base64url(crypto.createHmac('sha256', 'dev-only-signing-key-change-before-production-123456').update(encodedHeader + '.' + encodedPayload).digest());
console.log(encodedHeader + '.' + encodedPayload + '.' + signature);
