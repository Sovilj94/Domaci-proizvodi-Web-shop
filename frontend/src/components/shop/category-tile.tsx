import Link from "next/link";
import Image from "next/image";
import { Sprout } from "lucide-react";
import type { Category } from "@/types/domain";

/**
 * Pločica kategorije sa mockupa: kvadratna slika sa zaobljenim uglovima, naziv ispod,
 * blago uvećanje na hover. Koriste je početna („Popularne kategorije") i /kategorije.
 */
export function CategoryTile({ category }: { category: Category }) {
  return (
    <Link href={`/products?category=${category.slug}`} className="group block text-center">
      <span className="relative block aspect-square overflow-hidden rounded-card bg-sand">
        {category.imageUrl ? (
          <Image
            src={category.imageUrl}
            alt=""
            fill
            sizes="(max-width: 640px) 33vw, 150px"
            className="object-cover transition duration-500 group-hover:scale-105"
          />
        ) : (
          <span className="absolute inset-0 grid place-items-center">
            <Sprout className="h-8 w-8 text-brand-400" />
          </span>
        )}
      </span>
      <span className="mt-2 block text-xs font-medium text-ink transition group-hover:text-brand-700 sm:text-sm">
        {category.name}
      </span>
    </Link>
  );
}
