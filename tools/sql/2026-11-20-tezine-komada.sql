-- Procijenjene težine jednog komada (kg) za artikle na komad — za raspon težine korpica.
-- Procjena, 20.11.2026: tipičan komad / veza / tegla na pijaci. Ispravke idu u
-- Admin → Korpice → Postavke → Artikli → „Kg po komadu".
-- Upisuje samo gdje težina još nije unesena; artikle traži po nazivu (radi i na Neonu).

UPDATE "Products" p SET "TezinaKomada" = v.kg, "UpdatedAt" = now()
FROM (VALUES
    -- povrće
    ('Bundeva',                  2.5),   -- obična bundeva; teža od 2,5 kg ne staje ni u veliku korpicu (¼ od 10 kg)
    ('Bundeva hokaido',          1.2),
    ('Celer',                    0.5),   -- korijen
    ('Crvena salata',            0.3),
    ('Karfiol',                  1.0),
    ('Keleraba',                 0.3),
    ('Kineski kupus',            1.0),
    ('Kopriva',                  0.2),   -- veza
    ('Kukuruz šećerac',          0.3),   -- klip
    ('Mladi bijeli luk',         0.2),   -- veza
    ('Mladi crni luk',           0.3),   -- veza
    ('Mladi luk',                0.15),  -- veza
    ('Pak čoj',                  0.3),
    ('Peršun',                   0.1),   -- veza
    ('Peršun list',              0.05),  -- veza
    ('Rotkvica',                 0.25),  -- veza
    ('Rukola',                   0.1),   -- veza
    ('Salata kristalka',         0.4),
    ('Sremuš',                   0.1),   -- veza
    ('Zelena salata',            0.3),
    -- voće
    ('Dinja',                    1.5),
    ('Lubenica',                 6.0),
    ('Sušena aronija',           0.2),   -- pakovanje
    ('Sušene jabuke',            0.2),
    ('Sušene kruške',            0.2),
    -- Špajz
    ('Domaći ajvar',             0.6),   -- tegla 0,55 kg + staklo
    ('Džem od šljive',           0.4),
    ('Jaja iz slobodnog uzgoja', 0.06)
) AS v(naziv, kg)
WHERE p."Name" = v.naziv
  AND p."Unit" = 'Kom'
  AND p."TezinaKomada" IS NULL;
