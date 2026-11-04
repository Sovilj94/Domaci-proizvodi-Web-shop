# Fiskalizacija — dizajn (priprema, probni dobavljač)

Odobreno 04.11.2026: „kreni sa probnim dobavljačem; nismo u PDV-u ali ćemo biti; nismo još ni počeli".
Pravila RS (Zakon o fiskalnim sistemima: ESIR + PFR + bezbjednosni element) treba potvrditi sa
knjigovođom i izabranim dobavljačem ESIR-a. Aplikacija NE izdaje fiskalni račun sama — predaje
podatke certifikovanom dobavljaču preko interfejsa `IFiskalniDobavljac`.

## Kada
- Prometni račun nastaje kad narudžba pređe u **Isporučeno** (plaćanje pouzećem = naplata na vratima),
  iz vozačeve aplikacije ili admin panela. Jedan po narudžbi. Neuspjela isporuka i otkazivanje ne prave račun.
- Račun se upisuje kao **NaCekanju** u istom upisu kao promjena statusa (ne gubi se ako dobavljač ne odgovara),
  a posao svakih 30 s ga šalje dobavljaču. Greška: ponovni pokušaj sa rastućim razmakom (1, 2, 5, 10, 30, 60 min);
  trajna greška (dobavljač kaže da je zahtjev neispravan) čeka admina („Pokušaj ponovo").
- **Refundacija**: admin, za izdat prometni račun; referiše broj i vrijeme prometnog. Jedna po narudžbi.

## Sadržaj
- Stavke = stavke narudžbe (korpica kao jedna stavka po svojoj cijeni) + „Dostava" ako je naplaćena.
- Promo popust i poeni se raspoređuju srazmjerno po stavkama (polje Popust na stavci); zbir = TotalAmount.
- Poreska oznaka po stavci iz podešavanja: obveznik PDV-a → oznaka PDV-a (stopa), inače oznaka „nije u PDV-u".
  Oznake i stopa su u bazi (Podesavanja), mijenja ih admin; podrazumijevano neobveznik.
- Stavke se snimaju u trenutku nastanka računa (JSON) — kasnije izmjene artikala ih ne diraju.
- Način plaćanja: gotovina (jedini danas).

## Dobavljač
- `Fiskalizacija:Dobavljac` u konfiguraciji: `probni` (jedini zasad). Probni vraća broj „PROBNI-…" i QR tekst
  „nema fiskalnu vrijednost"; nikad ne kontaktira Poresku upravu. Pravi dobavljač se dodaje kao nova klasa.

## Admin
- `/admin/fiskalni-racuni`: podešavanja (PDV, stopa, oznake, dobavljač), sažetak (izdati / na čekanju / greške),
  lista po periodu sa statusom, pregled računa, „Pokušaj ponovo", „Refundiraj".

## Van opsega (sljedeći koraci)
Slanje računa kupcu mejlom/Viberom, kartično plaćanje, djelimična refundacija, pravi dobavljač, avansni računi.
