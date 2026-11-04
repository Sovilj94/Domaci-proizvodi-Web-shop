# Mejlovi spremni za produkciju — dizajn (2026-11-04)

## Zašto
- SMTP se šalje iz samog zahtjeva: spor server usporava kupca, pad = izgubljen mejl, nema evidencije.
- Render free blokira odlazni SMTP (25/465/587) — u produkciji danas ne ode nijedan mejl.
- Nema zaboravljene lozinke, nema obavještenja o statusu narudžbe, nema obavještenja radnicima, nema ručnog slanja.

## Odluke
1. **Red (outbox) `EmailPoruka`** — svaki mejl je red u bazi (primalac, naslov, HTML, vrsta, status, pokušaji,
   sljedeći pokušaj, greška, ključ). `SlanjeEmailovaService` šalje odmah (signal) i svakih 15 s, po 20.
   Razmak ponovnih pokušaja 1, 2, 5, 10, 30, 60 min; poslije 10 pokušaja ili trajne greške dobavljača → „Greška"
   (admin može „Pošalji ponovo"). **Ključ** (jedinstven) sprečava duple mejlove (npr. `odbijena:{orderId}`).
2. **Transport bira konfiguracija** `Email:Transport`: `smtp` (dev Mailpit, ili bilo koji SMTP) ili `brevo`
   (HTTP API, port 443 — radi na Renderu; besplatno 300 mejlova dnevno, EU). Nepoznat naziv ruši start.
3. **Zahtjev gosta** više ne pada kad SMTP padne: mejl je u redu i ide odmah; ako dobavljač ne odgovori, ide
   ponovo za minut. Kupac vidi „poslali smo link" — red garantuje da će otići čim dobavljač proradi.
4. **Obavještenja kupcu** (svako se gasi u podešavanjima): narudžba potvrđena, isporučena (link za ocjenu),
   nije uručena, otkazana (admin) i odbijena (nije potvrđena do roka).
5. **Obavještenja radnicima** — primaoci: lista adresa u podešavanjima; prazna lista = svi admini.
   Nova narudžba, sažetak automatski odbijenih, fiskalni račun sa trajnom greškom, bitno odstupanje goriva,
   alarm sadržaja (već postoji). **Vozaču**: plan za sutra (stanice, zone, km i gorivo) u podešeni sat (17 h).
6. **Ručno slanje** (`/admin/mejlovi`): grupa primalaca — kupci sa narudžbom za sedmicu, aktivni pretplatnici,
   svi kupci sa nalogom, prijavljeni na novosti, vozači, radnici. Prvo pregled (broj i primjeri), pa slanje.
   „Svi kupci" i „novosti" imaju link za odjavu (HMAC potpis adrese, bez tabele tokena); servisne grupe
   (sedmica, pretplatnici) ga nemaju — to su obavještenja o usluzi koju je kupac naručio.
7. **Zaboravljena lozinka**: token 32 B, u bazi samo SHA-256, važi 1 h, jednokratan; odgovor uvijek isti
   (ne otkriva da li adresa postoji).
8. **Pregled poslatog** (`/admin/mejlovi`): status, vrsta, primalac, greška, pregled HTML-a, ponovno slanje,
   probni mejl na svoju adresu.

## Van obima
Praćenje otvaranja/klikova, HTML editor kampanja, zakazano slanje kampanja, SMS/Viber.
