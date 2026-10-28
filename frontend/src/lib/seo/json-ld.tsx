import type { Korpica, Producer, Product, Recept } from "@/types/domain";
import { KONTAKT } from "@/lib/kontakt";
import { absoluteUrl, OG_IMAGE, SITE_DESCRIPTION, SITE_NAME, SITE_URL } from "./site";

/**
 * Strukturirani podaci (schema.org, JSON-LD) za Google rich rezultate. Serverska komponenta:
 * završi u HTML-u odmah, bez čekanja na hidrataciju.
 */
export function JsonLd({ data }: { data: object | object[] }) {
  // `<` se escape-uje da tekst iz baze ne može zatvoriti <script> tag.
  const json = JSON.stringify(data).replace(/</g, "\\u003c");
  return <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: json }} />;
}

const ORG_ID = `${SITE_URL}/#organizacija`;

/** Trgovina + sajt; ide u layout prodavnice, pa važi za svaku javnu stranicu. */
export function sajtLd() {
  return [
    {
      "@context": "https://schema.org",
      "@type": "GroceryStore",
      "@id": ORG_ID,
      name: SITE_NAME,
      description: SITE_DESCRIPTION,
      url: SITE_URL,
      logo: absoluteUrl("/favicon.ico"),
      image: absoluteUrl(OG_IMAGE.url),
      telephone: KONTAKT.telefon,
      priceRange: "KM",
      currenciesAccepted: "BAM",
      paymentAccepted: "Gotovina (pouzećem)",
      address: {
        "@type": "PostalAddress",
        addressLocality: "Banja Luka",
        addressRegion: "Republika Srpska",
        addressCountry: "BA",
      },
      areaServed: { "@type": "City", name: "Banja Luka" },
    },
    {
      "@context": "https://schema.org",
      "@type": "WebSite",
      name: SITE_NAME,
      url: SITE_URL,
      inLanguage: "sr-Latn",
      publisher: { "@id": ORG_ID },
      potentialAction: {
        "@type": "SearchAction",
        target: { "@type": "EntryPoint", urlTemplate: `${SITE_URL}/spajz?search={search_term_string}` },
        "query-input": "required name=search_term_string",
      },
    },
  ];
}

export function mrvicaLd(stavke: { naziv: string; putanja: string }[]) {
  return {
    "@context": "https://schema.org",
    "@type": "BreadcrumbList",
    itemListElement: stavke.map((s, i) => ({
      "@type": "ListItem",
      position: i + 1,
      name: s.naziv,
      item: absoluteUrl(s.putanja),
    })),
  };
}

export function proizvodLd(p: Product) {
  return {
    "@context": "https://schema.org",
    "@type": "Product",
    name: p.name,
    description: p.description || undefined,
    image: p.imageUrl ? [absoluteUrl(p.imageUrl)] : undefined,
    sku: p.id,
    category: p.categoryName,
    brand: p.producer ? { "@type": "Brand", name: p.producer.name } : undefined,
    offers: {
      "@type": "Offer",
      url: absoluteUrl(`/products/${p.slug}`),
      priceCurrency: "BAM",
      price: p.price.toFixed(2),
      availability:
        p.isAvailable && p.stock > 0 ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
      itemCondition: "https://schema.org/NewCondition",
      seller: { "@id": ORG_ID },
      areaServed: "Banja Luka",
    },
  };
}

export function korpicaLd(k: Korpica, putanja: string) {
  return {
    "@context": "https://schema.org",
    "@type": "Product",
    name: k.naziv,
    description: k.opis || undefined,
    image: k.imageUrl ? [absoluteUrl(k.imageUrl)] : undefined,
    sku: k.id,
    category: "Korpice",
    offers: {
      "@type": "Offer",
      url: absoluteUrl(putanja),
      priceCurrency: "BAM",
      price: k.cijena.toFixed(2),
      availability: k.dostupna ? "https://schema.org/InStock" : "https://schema.org/OutOfStock",
      seller: { "@id": ORG_ID },
      areaServed: "Banja Luka",
    },
  };
}

export function receptLd(r: Recept) {
  return {
    "@context": "https://schema.org",
    "@type": "Recipe",
    name: r.naslov,
    description: r.opis || undefined,
    image: r.slikaUrl ? [absoluteUrl(r.slikaUrl)] : undefined,
    author: { "@id": ORG_ID, "@type": "Organization", name: SITE_NAME },
    totalTime: r.vrijemeMinuta > 0 ? `PT${r.vrijemeMinuta}M` : undefined,
    recipeYield: r.porcije > 0 ? `${r.porcije} porcija` : undefined,
    recipeIngredient: r.sastojci.map((s) => [s.kolicina, s.naziv].filter(Boolean).join(" ")),
    recipeInstructions: r.koraci.map((k) => ({ "@type": "HowToStep", text: k })),
    inLanguage: "sr-Latn",
  };
}

export function domacinLd(p: Producer) {
  return {
    "@context": "https://schema.org",
    "@type": "Organization",
    name: p.name,
    description: p.description || undefined,
    image: p.photoUrl ? absoluteUrl(p.photoUrl) : undefined,
    address: { "@type": "PostalAddress", addressLocality: p.village, addressCountry: "BA" },
    url: absoluteUrl(`/proizvodjaci/${p.id}`),
    memberOf: { "@id": ORG_ID },
  };
}

export function pitanjaLd(pitanja: { pitanje: string; odgovor: string }[]) {
  return {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    mainEntity: pitanja.map((p) => ({
      "@type": "Question",
      name: p.pitanje,
      acceptedAnswer: { "@type": "Answer", text: p.odgovor },
    })),
  };
}
