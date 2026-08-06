import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

/** Merge conditional class names and resolve Tailwind conflicts. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

const priceFmt = new Intl.NumberFormat("bs-BA", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** Money in Bosnian convertible marks (BAM), rendered as "12,50 KM". */
export function formatPrice(amount: number): string {
  return `${priceFmt.format(amount)} KM`;
}

/** "/kg", "/kom", "/l" — the per-unit suffix shown next to a price. */
export function unitSuffix(unit: string): string {
  return `/${unit}`;
}

const dateFmt = new Intl.DateTimeFormat("bs-BA", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
});

const dateTimeFmt = new Intl.DateTimeFormat("bs-BA", {
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

export function formatDate(iso: string): string {
  return dateFmt.format(new Date(iso));
}

export function formatDateTime(iso: string): string {
  return dateTimeFmt.format(new Date(iso));
}

const WEEKDAYS_BS = [
  "Nedjelja",
  "Ponedjeljak",
  "Utorak",
  "Srijeda",
  "Četvrtak",
  "Petak",
  "Subota",
];

export function weekdayName(dayOfWeek: number): string {
  return WEEKDAYS_BS[dayOfWeek] ?? "";
}

/** Slugify a Latin/BHS string for product/category slugs. */
export function slugify(value: string): string {
  const map: Record<string, string> = {
    č: "c", ć: "c", đ: "dj", š: "s", ž: "z",
    Č: "c", Ć: "c", Đ: "dj", Š: "s", Ž: "z",
  };
  return value
    .split("")
    .map((ch) => map[ch] ?? ch)
    .join("")
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}
