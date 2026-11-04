# Mejlovi u produkciji — šta treba uraditi

Kod je spreman, a ostaje posao kod tri strane: **registrar domena**, **Brevo** i **Render**. Ovdje je sve
redom, sa kvačicama. Oko sat vremena rada, plus čekanje da se DNS raširi (obično do sat, rijetko do 24 h).

Kratko o tome kako radi: svaki mejl se prvo upiše u bazu (red), a pozadinski posao ga šalje. Ako slanje ne
uspije, pokušava ponovo za 1, 2, 5, 10, 30 pa 60 minuta. Zato ništa ne propada dok se ovo podešava — mejlovi
čekaju u redu i vide se na **Admin → Mejlovi**.

---

## 0. Prije svega

- [ ] **Vlastiti domen** (npr. `epiljara.ba`). Bez njega mejlovi idu u spam.
  - Sa adrese na `vercel.app` ili `onrender.com` ne može se slati.
  - Gmail adresa kao pošiljalac ne prolazi: Gmail-ov DMARC odbija mejlove „od Gmaila" poslate preko drugog servisa.
  - `.ba` domen se kupuje kod ovlaštenog registrara za BiH; `.com` kod bilo kojeg (Namecheap, Cloudflare…).
- [ ] **Sanduče koje neko čita** za odgovore kupaca (Reply-To), npr. `info@epiljara.ba`. Može i:
  - besplatno prosljeđivanje kod registrara (ili ImprovMX) na tvoj Gmail, ili
  - pravo sanduče (Zoho Mail free, Google Workspace…).

  Za slanje preko Brevoa sanduče nije potrebno, samo za primanje odgovora.
- [ ] **Grana `mejlovi` spojena u `main`** i deployana (reci „push na main"). Do tada produkcija nema ovaj kod.

## 1. Brevo nalog

- [ ] Registracija na <https://www.brevo.com> (besplatni plan: 300 mejlova dnevno, bez kartice).
- [ ] Pri registraciji Brevo pita za firmu i adresu. Upiši prave podatke, jer Brevo ih stavlja u podnožje marketinških mejlova i provjerava nove naloge.
- [ ] Ako Brevo traži aktivaciju transakcionih mejlova (*Transactional* → *Activate*), potvrdi je. Novi nalozi ponekad čekaju odobrenje od nekoliko sati.

## 2. Domen u Brevu (najvažniji korak)

- [ ] Brevo → ikona profila → **Senders, Domains & Dedicated IPs** → **Domains** → **Add a domain** → upiši `epiljara.ba`.
- [ ] Brevo pokaže zapise. Dodaj ih kod registrara (DNS podešavanja domena), tačno kako ih Brevo prikaže:

  | Zapis | Tip | Šta radi |
  | --- | --- | --- |
  | `brevo-code:…` | TXT na korijenu domena | potvrda da je domen tvoj |
  | DKIM (`mail._domainkey` ili slično) | TXT ili CNAME, kako Brevo ponudi | potpis da mejl nije izmijenjen |
  | SPF | TXT na korijenu: `v=spf1 include:spf.brevo.com ~all` | Brevo smije slati u ime domena |
  | DMARC | TXT na `_dmarc`: `v=DMARC1; p=none; rua=mailto:info@epiljara.ba` | izvještaji i pravila za lažne mejlove |

  **Pazi:**
  - **SPF smije biti samo jedan.** Ako domen već ima `v=spf1 …` (npr. od sandučeta), ne pravi drugi, nego dopiši `include:spf.brevo.com` u postojeći, npr. `v=spf1 include:_spf.google.com include:spf.brevo.com ~all`.
  - **Ovi zapisi ne diraju prijem pošte** (MX zapise), pa postojeće sanduče radi kao i prije.
  - **DMARC sa `p=none`** samo prati. Kad mejlovi sedmicama prolaze bez problema, možeš ga pooštriti na `p=quarantine`.
- [ ] U Brevu klikni **Authenticate / Verify**. Sve stavke treba da budu zelene. Ako nisu, sačekaj sat pa probaj ponovo, jer se DNS sporo širi.

## 3. Pošiljalac

- [ ] Brevo → **Senders** → **Add a sender**:
  - ime: `E piljara`;
  - adresa: npr. `narudzbe@epiljara.ba` (mora biti na domenu iz koraka 2; sanduče za nju ne mora postojati).

## 4. API ključ

- [ ] Brevo → **SMTP & API** → kartica **API Keys** → **Generate a new API key** → naziv `epiljara-render`.
- [ ] **Kopiraj ključ odmah**, jer se prikazuje samo jednom. Ne šalji ga u chat i ne stavljaj ga u repo.
- [ ] Ako Brevo nudi **Authorized IPs**, ne uključuj tu zaštitu: Render nema stalnu IP adresu, pa bi slanje bilo odbijeno.

## 5. Render

Render → servis **epiljara-api** → **Environment** → dodaj ili izmijeni:

- [ ] `Email__Brevo__ApiKey` = ključ iz koraka 4
- [ ] `Email__From` = adresa iz koraka 3 (npr. `narudzbe@epiljara.ba`)
- [ ] `Email__FromName` = `E piljara`
- [ ] `Email__ReplyTo` = sanduče koje čitaš (npr. `info@epiljara.ba`)
- [ ] `App__BaseUrl` = javna adresa sajta (npr. `https://epiljara.ba`, bez `/` na kraju). Od nje su linkovi u mejlovima.
- [ ] **Posljednje:** `Email__Transport` = `brevo`

`Email__Transport=brevo` ide tek kad je ključ unesen. API sa Brevom bez ključa namjerno ne starta, da ne bi tiho radio bez mejlova.

- [ ] **Save Changes**. Render sam radi redeploy (2–5 min).
- [ ] Na `https://epiljara-api.onrender.com/health` mora pisati `{"status":"ok"}`. Ako ne piše, pogledaj Render → **Logs**: tamo je poruka šta fali.

## 6. Provjera

- [ ] Admin → **Mejlovi**. Žuta traka „Mejlovi idu preko SMTP-a…" više ne smije stajati, a uz karticu „Poslato danas" piše `dobavljač: brevo`.
- [ ] **Pošalji probni mejl** na svoj Gmail.
  - U tabeli status treba da bude „Poslato" za nekoliko sekundi.
  - U Gmailu otvori mejl → ⋮ → **Show original**. Mora pisati `SPF: PASS`, `DKIM: PASS`, `DMARC: PASS`.
  - Ako je mejl u spamu, vrati se na korak 2.
- [ ] Probaj i pravi tok:
  - registracija novog naloga (mejl za potvrdu);
  - „Zaboravljena lozinka" na prijavi;
  - jedna narudžba (potvrda kupcu i obavještenje radnicima).
- [ ] Ako su se mejlovi nakupili u redu dok Brevo nije bio podešen:
  - oni „Na čekanju" odu sami;
  - one sa „Greška" otvori i klikni **Pošalji ponovo**.

## 7. Podešavanja u adminu

Admin → **Mejlovi** → „Automatska obavještenja":

- [ ] **Primaoci obavještenja radnicima**: upiši adrese, odvojene zarezom (npr. vlasnik, pakovanje). Prazno znači svi admini.
- [ ] Isključi obavještenja koja ti ne trebaju (npr. „Nova narudžba", ako ih ima mnogo).
- [ ] **Plan za sutra vozačima**: sat slanja (podrazumijevano 17). Vozač dobija plan na adresu svog naloga za aplikaciju, pa ona mora biti prava.

## 8. Render free spava (preporuka)

Render free servis zaspi poslije ~15 minuta bez posjeta.

- **Kad spava:** ne šalje plan vozačima u 17 h i ne ponavlja neuspjele mejlove, sve do prve posjete.
- **Mejlovi iz zahtjeva** (narudžba, registracija) idu odmah, jer je servis tada budan.

- [ ] Besplatan ping:
  - napravi nalog na <https://cron-job.org>;
  - novi posao za URL `https://epiljara-api.onrender.com/health`, svakih 10 minuta.

  Jedan servis budan cijeli mjesec staje u 750 besplatnih Render sati.

## 9. Ograničenja i kad preći na plaćeni plan

- **300 mejlova dnevno besplatno**, i to zajedno: transakcioni mejlovi i ručna obavještenja. Na „Mejlovi" kartica „Poslato danas" pokazuje koliko je potrošeno.
- **Grube brojke:** jedna narudžba troši oko 3–4 mejla (potvrda, potvrđena, isporučena, radnici). 300 dnevno je dovoljno za ~70 narudžbi dnevno bez ručnih obavještenja.
- **Ručno obavještenje „svim kupcima"** može samo potrošiti dnevnu kvotu. Stranica upozori kad grupa ima više od 300. Šta ne stane ide narednih dana, jer red ponavlja sam, ali tog dana tada ni potvrde narudžbi ne prolaze. Veće slanje radi uveče, ili pređi na plaćeni plan (Brevo „Starter", ~9 €/mj za 5.000 mejlova mjesečno, bez dnevnog limita).

## Kad nešto ne radi

| Šta vidiš uz mejl (Admin → Mejlovi) | Uzrok | Šta uraditi |
| --- | --- | --- |
| `Brevo 401 … Key not found` | pogrešan ili obrisan ključ | novi ključ (korak 4) → `Email__Brevo__ApiKey` na Renderu |
| `Brevo 401 … unauthorized` / `IP` | uključen „Authorized IPs" | isključi ga u Brevu (korak 4) |
| `Brevo 400 … sender … not valid` | `Email__From` nije potvrđen pošiljalac | korak 3; adresa mora biti identična |
| `Brevo 400 … email is not valid` | kupac je upisao pogrešnu adresu | ništa — mejl je „Greška" i ne ponavlja se |
| `Brevo 402` / `429` | potrošena dnevna kvota ili previše odjednom | čeka se — red ponavlja sam; ili plaćeni plan |
| Mejl „Poslato", ali ga kupac nema | spam ili pogrešna adresa | Gmail „Show original" (korak 6); Brevo → **Transactional** → **Logs** po adresi |
| API ne starta poslije izmjene | `Email__Transport=brevo` bez ključa ili pogrešan naziv | Render → Logs → poruka; provjeri korak 5 |

## Lokalno (razvoj)

Ništa od ovoga ne treba lokalno: `Email:Transport` je `smtp`, a mejlove hvata Mailpit.

```
docker start mailpit        # ili prvi put: docker run -d --name mailpit -p 1025:1025 -p 8025:8025 axllent/mailpit
```

Pregled mejlova je na <http://localhost:8025>.
