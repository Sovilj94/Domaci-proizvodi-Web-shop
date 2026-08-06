import Link from "next/link";
import { Clock, MapPin, Phone } from "lucide-react";
import { Logo } from "@/components/layout/logo";

export function ShopFooter() {
  return (
    <footer className="mt-20 border-t border-black/5 bg-white">
      <div className="mx-auto grid max-w-6xl gap-10 px-4 py-12 sm:grid-cols-2 lg:grid-cols-4">
        <div className="space-y-3">
          <Logo />
          <p className="max-w-xs text-sm text-muted">
            Domaći proizvodi sa Manjače, spakovani i dostavljeni na tvoja vrata u Banjoj Luci.
          </p>
        </div>

        <div>
          <h3 className="mb-3 font-display text-sm font-bold text-ink">Kupovina</h3>
          <ul className="space-y-2 text-sm text-muted">
            <li><Link href="/products" className="hover:text-ink">Svi proizvodi</Link></li>
            <li><Link href="/cart" className="hover:text-ink">Korpa</Link></li>
            <li><Link href="/orders" className="hover:text-ink">Moje narudžbe</Link></li>
          </ul>
        </div>

        <div id="dostava">
          <h3 className="mb-3 font-display text-sm font-bold text-ink">Dostava</h3>
          <ul className="space-y-2 text-sm text-muted">
            <li className="flex items-center gap-2"><Clock className="h-4 w-4 text-brand-500" /> Narudžbe do utorka u 12h</li>
            <li className="flex items-center gap-2"><MapPin className="h-4 w-4 text-brand-500" /> Centar · Borik · Lauš · Starčevica</li>
            <li className="pl-6">Dostava četvrtkom (petak po potrebi)</li>
          </ul>
        </div>

        <div>
          <h3 className="mb-3 font-display text-sm font-bold text-ink">Kontakt</h3>
          <ul className="space-y-2 text-sm text-muted">
            <li className="flex items-center gap-2"><Phone className="h-4 w-4 text-brand-500" /> +387 51 000 000</li>
            <li className="flex items-center gap-2"><MapPin className="h-4 w-4 text-brand-500" /> Manjača, Banja Luka</li>
          </ul>
        </div>
      </div>
      <div className="border-t border-black/5 py-5">
        <p className="mx-auto max-w-6xl px-4 text-xs text-muted">
          © {new Date().getFullYear()} E piljara. Svježe sa Manjače.
        </p>
      </div>
    </footer>
  );
}
