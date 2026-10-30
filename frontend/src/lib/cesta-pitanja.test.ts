import { test } from "node:test";
import assert from "node:assert/strict";
import { naJeziku, popuniOdgovor } from "./cesta-pitanja.ts";

test("oznake se popunjavaju brojevima sa servera", () => {
  assert.equal(
    popuniOdgovor("Plan nosi obavezu na {obavezaMjesecna}, odnosno {obavezaTriMjeseca} isporuka.", {
      obaveza: { mjesecna: 4, triMjeseca: 12 },
    }),
    "Plan nosi obavezu na 4, odnosno 12 isporuka.",
  );
});

test("rečenica bez podatka sa servera otpada — nikad izmišljen broj", () => {
  assert.equal(
    popuniOdgovor("Stiže svake sedmice. Jeftinije {popustNedjeljna}% uz nedjeljni plan. Pauzu podešavaš u nalogu.", {}),
    "Stiže svake sedmice. Pauzu podešavaš u nalogu.",
  );
});

test("engleska verzija iz baze kad postoji, inače srpska", () => {
  const p = { pitanje: "Kako se plaća?", odgovor: "Pouzećem.", pitanjeEn: "How do I pay?" };
  assert.deepEqual(naJeziku(p, true), { pitanje: "How do I pay?", odgovor: "Pouzećem." });
  assert.deepEqual(naJeziku(p, false), { pitanje: "Kako se plaća?", odgovor: "Pouzećem." });
});
