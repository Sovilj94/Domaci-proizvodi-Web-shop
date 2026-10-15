import type {
  AuthUser,
  Category,
  DeliveryRoute,
  DeliverySlot,
  DeliveryZone,
  Order,
  OrderItem,
  OrderStatus,
  Producer,
  Product,
  Warehouse,
} from "@/types/domain";
import type {
  AssignRouteRequest,
  CreateOrderRequest,
  DashboardStats,
  Driver,
  ProductInput,
} from "@/lib/api/contracts";

/*
 * In-memory mock database standing in for the ASP.NET Core + PostgreSQL backend.
 * State lives on globalThis so it survives Next.js hot-reload in development.
 * Delete src/server/mock and point NEXT_PUBLIC_API_BASE_URL at the real API to go live.
 */

interface StoredUser extends AuthUser {
  password: string;
}

interface Db {
  categories: Category[];
  producers: Producer[];
  products: Product[];
  zones: DeliveryZone[];
  slots: DeliverySlot[];
  drivers: Driver[];
  orders: Order[];
  routes: DeliveryRoute[];
  users: StoredUser[];
  /** Jedna vrijednost, kao i u pravoj bazi (tabela "Magacin" ima samo jedan red). */
  warehouse: Warehouse;
  orderSeq: number;
}

const img = (seed: string) => `https://picsum.photos/seed/${seed}/700/700`;

function seed(): Db {
  const categories: Category[] = [
    { id: "cat-povrce", name: "Povrće", slug: "povrce", sortOrder: 1, imageUrl: img("povrce") },
    { id: "cat-voce", name: "Voće", slug: "voce", sortOrder: 2, imageUrl: img("voce") },
    { id: "cat-mlijecni", name: "Mliječni proizvodi", slug: "mlijecni-proizvodi", sortOrder: 3, imageUrl: img("mlijeko") },
    { id: "cat-meso", name: "Meso i suhomesnato", slug: "meso", sortOrder: 4, imageUrl: img("meso") },
    { id: "cat-jaja", name: "Jaja", slug: "jaja", sortOrder: 5, imageUrl: img("jaja") },
    { id: "cat-med", name: "Med i pčelinji proizvodi", slug: "med", sortOrder: 6, imageUrl: img("med") },
    { id: "cat-zimnica", name: "Zimnica i džemovi", slug: "zimnica", sortOrder: 7, imageUrl: img("zimnica") },
  ];

  const producers: Producer[] = [
    {
      id: "prod-kovacevic",
      name: "Porodica Kovačević",
      village: "Han Kola",
      description:
        "Treća generacija koja obrađuje zemlju na obroncima Manjače. Povrće bez pesticida, ubrano istog jutra kad ide na dostavu.",
      photoUrl: img("farm-kovacevic"),
      latitude: 44.6321,
      longitude: 17.0954,
      phone: "+387 65 111 222",
      artikli: [],
    },
    {
      id: "prod-maric",
      name: "OPG Marić",
      village: "Stričići",
      description:
        "Mala porodična mljekara. Krave na ispaši cijelo ljeto, sir i kajmak po receptu koji se ne mijenja 40 godina.",
      photoUrl: img("farm-maric"),
      latitude: 44.667,
      longitude: 17.02,
      phone: "+387 66 333 444",
      artikli: [],
    },
    {
      id: "prod-jovanovic",
      name: "Gazdinstvo Jovanović",
      village: "Rekavice",
      description:
        "Pčelinjak sa 120 košnica i voćnjak starih sorti jabuka i šljiva. Med, džemovi i rakija iz vlastite proizvodnje.",
      photoUrl: img("farm-jovanovic"),
      latitude: 44.6,
      longitude: 17.15,
      phone: "+387 65 555 666",
      artikli: [],
    },
    {
      id: "prod-savic",
      name: "Domaćinstvo Savić",
      village: "Bronzani Majdan",
      description:
        "Živina i jaja iz slobodnog uzgoja, domaća suha slanina i kobasice sušene na bukovom dimu.",
      photoUrl: img("farm-savic"),
      latitude: 44.72,
      longitude: 16.95,
      phone: "+387 66 777 888",
      artikli: [],
    },
  ];

  const p = (
    id: string,
    name: string,
    price: number,
    unit: Product["unit"],
    stock: number,
    categoryId: string,
    producerId: string,
    description: string,
    opts: { featured?: boolean; available?: boolean } = {},
  ): Product => {
    const cat = categories.find((c) => c.id === categoryId)!;
    const producer = producers.find((x) => x.id === producerId)!;
    const slug = id.replace(/^p-/, "");
    return {
      id,
      name,
      slug,
      price,
      unit,
      stock,
      isAvailable: opts.available ?? true,
      categoryId,
      categoryName: cat.name,
      categorySlug: cat.slug,
      producerId,
      producer: { id: producer.id, name: producer.name, village: producer.village, photoUrl: producer.photoUrl },
      imageUrl: img(slug),
      description,
      isFeatured: opts.featured ?? false,
    };
  };

  const products: Product[] = [
    p("p-krompir", "Domaći krompir", 1.2, "kg", 400, "cat-povrce", "prod-kovacevic", "Sorta desiree, čuvana u zemljanom podrumu. Idealan za pečenje i variva.", { featured: true }),
    p("p-paradajz", "Paradajz iz plastenika", 3.5, "kg", 80, "cat-povrce", "prod-kovacevic", "Mesnati paradajz pun ukusa, bran zrio — ne skladišni.", { featured: true }),
    p("p-paprika", "Babura paprika", 4.0, "kg", 60, "cat-povrce", "prod-kovacevic", "Debela mesnata babura, savršena za punjenje ili ajvar."),
    p("p-luk", "Crni luk", 1.5, "kg", 200, "cat-povrce", "prod-kovacevic", "Ljutkasti domaći luk, dugo se čuva."),
    p("p-mrkva", "Mrkva", 2.0, "kg", 120, "cat-povrce", "prod-kovacevic", "Slatka mrkva iz bašte, bez voska i premaza."),
    p("p-jabuka", "Jabuka idared", 2.2, "kg", 150, "cat-voce", "prod-jovanovic", "Hrskava i sočna, iz voćnjaka na 700m nadmorske visine.", { featured: true }),
    p("p-sljiva", "Šljiva požegača", 2.8, "kg", 90, "cat-voce", "prod-jovanovic", "Klasična rakijska i džem šljiva, slatka kao med."),
    p("p-sir", "Mladi kravlji sir", 9.0, "kg", 40, "cat-mlijecni", "prod-maric", "Punomasni sir od jutarnjeg pomuza, blago slan.", { featured: true }),
    p("p-kajmak", "Kajmak", 14.0, "kg", 25, "cat-mlijecni", "prod-maric", "Zreli kajmak sa ispaše — pravi domaći doručak."),
    p("p-pavlaka", "Domaća pavlaka", 6.0, "l", 30, "cat-mlijecni", "prod-maric", "Gusta kisela pavlaka, kašika stoji uspravno."),
    p("p-mlijeko", "Svježe kravlje mlijeko", 2.5, "l", 60, "cat-mlijecni", "prod-maric", "Nehomogenizovano, prokuhati prije upotrebe."),
    p("p-jaja", "Jaja iz slobodnog uzgoja", 0.5, "kom", 600, "cat-jaja", "prod-savic", "Koke se šetaju dvorištem, žumance narandžasto.", { featured: true }),
    p("p-slanina", "Suha slanina", 18.0, "kg", 20, "cat-meso", "prod-savic", "Dimljena na bukovom dimu, sušena tri mjeseca."),
    p("p-kobasica", "Domaća kobasica", 16.0, "kg", 18, "cat-meso", "prod-savic", "Miješano svinjsko meso, ljuto po tradiciji."),
    p("p-med-livadski", "Livadski med", 12.0, "kg", 50, "cat-med", "prod-jovanovic", "Med sa manjačkih livada, kristališe prirodno — znak kvaliteta.", { featured: true }),
    p("p-med-bagrem", "Bagremov med", 15.0, "kg", 35, "cat-med", "prod-jovanovic", "Svijetli, blagi med, dugo ostaje tečan."),
    p("p-dzem-sljiva", "Džem od šljive", 7.0, "kom", 40, "cat-zimnica", "prod-jovanovic", "Tegla 720ml, kuhan bez konzervansa, 60% voća."),
    p("p-ajvar", "Domaći ajvar", 9.0, "kom", 45, "cat-zimnica", "prod-kovacevic", "Pečena paprika, blagi, tegla 500ml.", { available: false }),
  ];

  const zones: DeliveryZone[] = [
    { id: "zone-centar", name: "Centar", minOrderAmount: 20, deliveryFee: 3, isActive: true },
    { id: "zone-borik", name: "Borik", minOrderAmount: 20, deliveryFee: 3, isActive: true },
    { id: "zone-laus", name: "Lauš", minOrderAmount: 25, deliveryFee: 4, isActive: true },
    { id: "zone-starcevica", name: "Starčevica", minOrderAmount: 25, deliveryFee: 4, isActive: true },
  ];

  const slots: DeliverySlot[] = [
    { id: "slot-cet", dayOfWeek: 4, timeFrom: "16:00", timeTo: "20:00", cutoffHours: 48, label: "Četvrtak 16–20h" },
    { id: "slot-pet", dayOfWeek: 5, timeFrom: "16:00", timeTo: "20:00", cutoffHours: 48, label: "Petak 16–20h" },
  ];

  // maxStanica prati Driver.MaxStanicaPoDanu na backendu — mock i pravi API služe isti ugovor.
  const drivers: Driver[] = [
    { id: "drv-1", name: "Mladen (kombi 1)", phone: "+387 65 900 100", aktivan: true, maxStanica: 30 },
    { id: "drv-2", name: "Saša (kombi 2)", phone: "+387 65 900 200", aktivan: true, maxStanica: 30 },
  ];

  const users: StoredUser[] = [
    {
      id: "user-admin",
      name: "E piljara Admin",
      email: "admin@epiljara.ba",
      phone: "+387 51 000 000",
      role: "admin",
      password: "admin123",
    },
    {
      id: "user-marko",
      name: "Marko Marković",
      email: "marko@example.com",
      phone: "+387 65 123 456",
      address: "Kralja Petra I 12, Banja Luka",
      deliveryZoneId: "zone-centar",
      role: "customer",
      password: "marko123",
    },
  ];

  const db: Db = {
    categories,
    producers,
    products,
    zones,
    slots,
    drivers,
    orders: [],
    routes: [],
    users,
    // Iste koordinate koje migracija MagacinUBazi upisuje u pravu bazu — Stričići
    // na Manjači — da mock i backend ne polaze iz različitih tačaka.
    warehouse: { name: "Magacin Stričići", latitude: 44.577975, longitude: 17.005162 },
    orderSeq: 40,
  };

  // A couple of seed orders so the admin dashboard isn't empty on first run.
  seedOrder(db, "user-marko", "zone-centar", "slot-cet", "Kralja Petra I 12, Banja Luka", "Confirmed", [
    ["p-krompir", 5],
    ["p-sir", 1],
    ["p-jaja", 10],
  ]);
  seedOrder(db, "user-marko", "zone-borik", "slot-cet", "Bulevar 42, Banja Luka", "Packing", [
    ["p-med-livadski", 2],
    ["p-jabuka", 3],
  ]);
  seedOrder(db, "user-marko", "zone-starcevica", "slot-cet", "Carice Milice 8, Banja Luka", "Pending", [
    ["p-kajmak", 1],
    ["p-mlijeko", 4],
  ]);

  return db;
}

function nextCode(db: Db): string {
  db.orderSeq += 1;
  const yy = new Date().getFullYear().toString().slice(2);
  const mm = String(new Date().getMonth() + 1).padStart(2, "0");
  return `EP-${yy}${mm}-${String(db.orderSeq).padStart(4, "0")}`;
}

function seedOrder(
  db: Db,
  userId: string,
  zoneId: string,
  slotId: string,
  address: string,
  status: OrderStatus,
  lines: [string, number][],
): Order {
  const user = db.users.find((u) => u.id === userId)!;
  const zone = db.zones.find((z) => z.id === zoneId)!;
  const items: OrderItem[] = lines.map(([pid, qty], i) => {
    const prod = db.products.find((x) => x.id === pid)!;
    return {
      id: `oi-${db.orderSeq}-${i}`,
      productId: prod.id,
      productName: prod.name,
      unit: prod.unit,
      quantity: qty,
      unitPrice: prod.price,
      imageUrl: prod.imageUrl,
    };
  });
  const subtotal = round(items.reduce((s, it) => s + it.unitPrice * it.quantity, 0));
  const order: Order = {
    id: crypto.randomUUID(),
    code: nextCode(db),
    customerId: user.id,
    customerName: user.name,
    customerPhone: user.phone,
    status,
    items,
    subtotal,
    deliveryFee: zone.deliveryFee,
    popust: 0,
    iskoristeniPoeni: 0,
    poeniPopust: 0,
    totalAmount: round(subtotal + zone.deliveryFee),
    deliverySlotId: slotId,
    deliverySlot: db.slots.find((s) => s.id === slotId),
    deliveryZoneId: zone.id,
    deliveryZoneName: zone.name,
    deliveryAddress: address,
    createdAt: new Date().toISOString(),
    mozeSeOcijeniti: false,
  };
  db.orders.unshift(order);
  return order;
}

function round(n: number): number {
  return Math.round(n * 100) / 100;
}

/* ---------- singleton ---------- */

const g = globalThis as unknown as { __epiljaraDb?: Db };
export const db: Db = (g.__epiljaraDb ??= seed());

/* ---------- operations ---------- */

export function authenticate(email: string, password: string): AuthUser | null {
  const u = db.users.find(
    (x) => x.email.toLowerCase() === email.toLowerCase() && x.password === password,
  );
  if (!u) return null;
  return stripPassword(u);
}

export function registerUser(input: {
  name: string;
  email: string;
  phone: string;
  password: string;
  address?: string;
  deliveryZoneId?: string;
}): AuthUser | null {
  if (db.users.some((u) => u.email.toLowerCase() === input.email.toLowerCase())) {
    return null;
  }
  const user: StoredUser = {
    id: crypto.randomUUID(),
    name: input.name,
    email: input.email,
    phone: input.phone,
    address: input.address,
    deliveryZoneId: input.deliveryZoneId,
    role: "customer",
    password: input.password,
  };
  db.users.push(user);
  return stripPassword(user);
}

export function userByToken(token: string | null): AuthUser | null {
  if (!token) return null;
  const u = db.users.find((x) => x.id === token);
  return u ? stripPassword(u) : null;
}

function stripPassword(u: StoredUser): AuthUser {
  const { password: _pw, ...rest } = u;
  void _pw;
  return rest;
}

export function createOrder(
  req: CreateOrderRequest,
  actingUserId: string | null,
): { order?: Order; error?: string } {
  const zone = db.zones.find((z) => z.id === req.deliveryZoneId && z.isActive);
  if (!zone) return { error: "Nepoznata zona dostave." };
  const slot = db.slots.find((s) => s.id === req.deliverySlotId);
  if (!slot) return { error: "Nepoznat termin dostave." };
  if (!req.items?.length) return { error: "Korpa je prazna." };

  const items: OrderItem[] = [];
  for (const line of req.items) {
    const prod = db.products.find((x) => x.id === line.productId);
    if (!prod || !prod.isAvailable) {
      return { error: `Proizvod nije dostupan (${line.productId}).` };
    }
    if (line.quantity <= 0) return { error: "Neispravna količina." };
    items.push({
      id: crypto.randomUUID(),
      productId: prod.id,
      productName: prod.name,
      unit: prod.unit,
      quantity: line.quantity,
      unitPrice: prod.price, // snapshot
      imageUrl: prod.imageUrl,
    });
  }

  const subtotal = round(items.reduce((s, it) => s + it.unitPrice * it.quantity, 0));
  if (subtotal < zone.minOrderAmount) {
    return {
      error: `Minimalna narudžba za zonu ${zone.name} je ${zone.minOrderAmount} KM.`,
    };
  }

  const order: Order = {
    id: crypto.randomUUID(),
    code: nextCode(db),
    customerId: actingUserId ?? "guest",
    customerName: req.customer.name,
    customerPhone: req.customer.phone,
    status: "Pending",
    items,
    subtotal,
    deliveryFee: zone.deliveryFee,
    popust: 0,
    iskoristeniPoeni: 0,
    poeniPopust: 0,
    totalAmount: round(subtotal + zone.deliveryFee),
    deliverySlotId: slot.id,
    deliverySlot: slot,
    deliveryZoneId: zone.id,
    deliveryZoneName: zone.name,
    deliveryAddress: req.deliveryAddress,
    note: req.note,
    createdAt: new Date().toISOString(),
    mozeSeOcijeniti: false,
  };
  db.orders.unshift(order);
  return { order };
}

export function ordersForUser(userId: string): Order[] {
  return db.orders.filter((o) => o.customerId === userId);
}

export function updateOrderStatus(id: string, status: OrderStatus): Order | null {
  const order = db.orders.find((o) => o.id === id);
  if (!order) return null;
  order.status = status;
  return order;
}

export function upsertProduct(input: ProductInput, id?: string): Product | null {
  const cat = db.categories.find((c) => c.id === input.categoryId);
  const producer = db.producers.find((x) => x.id === input.producerId);
  if (!cat || !producer) return null;

  if (id) {
    const existing = db.products.find((x) => x.id === id);
    if (!existing) return null;
    Object.assign(existing, {
      name: input.name,
      price: input.price,
      unit: input.unit,
      stock: input.stock,
      categoryId: cat.id,
      categoryName: cat.name,
      categorySlug: cat.slug,
      producerId: producer.id,
      producer: { id: producer.id, name: producer.name, village: producer.village, photoUrl: producer.photoUrl },
      description: input.description,
      imageUrl: input.imageUrl || existing.imageUrl,
      isAvailable: input.isAvailable,
      isFeatured: input.isFeatured ?? existing.isFeatured,
    });
    return existing;
  }

  const slugBase = input.name;
  const product: Product = {
    id: crypto.randomUUID(),
    name: input.name,
    slug: slugFor(slugBase),
    price: input.price,
    unit: input.unit,
    stock: input.stock,
    isAvailable: input.isAvailable,
    categoryId: cat.id,
    categoryName: cat.name,
    categorySlug: cat.slug,
    producerId: producer.id,
    producer: { id: producer.id, name: producer.name, village: producer.village, photoUrl: producer.photoUrl },
    imageUrl: input.imageUrl || img(slugFor(slugBase)),
    description: input.description,
    isFeatured: input.isFeatured ?? false,
  };
  db.products.push(product);
  return product;
}

function slugFor(name: string): string {
  const map: Record<string, string> = { č: "c", ć: "c", đ: "dj", š: "s", ž: "z" };
  return name
    .toLowerCase()
    .split("")
    .map((ch) => map[ch] ?? ch)
    .join("")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
}

export function setProductAvailability(id: string, isAvailable: boolean): Product | null {
  const prod = db.products.find((x) => x.id === id);
  if (!prod) return null;
  prod.isAvailable = isAvailable;
  return prod;
}

export function dashboard(dateIso?: string): DashboardStats {
  const day = dateIso ? new Date(dateIso) : new Date();
  const sameDay = (iso: string) => {
    const d = new Date(iso);
    return (
      d.getFullYear() === day.getFullYear() &&
      d.getMonth() === day.getMonth() &&
      d.getDate() === day.getDate()
    );
  };
  const todays = db.orders.filter((o) => sameDay(o.createdAt));
  const revenueToday = round(
    todays
      .filter((o) => o.status !== "Cancelled")
      .reduce((s, o) => s + o.totalAmount, 0),
  );
  const breakdown = new Map<OrderStatus, number>();
  for (const o of db.orders) {
    breakdown.set(o.status, (breakdown.get(o.status) ?? 0) + 1);
  }
  return {
    date: day.toISOString(),
    ordersToday: todays.length,
    revenueToday,
    pendingOrders: db.orders.filter((o) => o.status === "Pending").length,
    outForDelivery: db.orders.filter((o) => o.status === "OutForDelivery").length,
    statusBreakdown: [...breakdown.entries()].map(([status, count]) => ({ status, count })),
  };
}

export function assignRoute(req: AssignRouteRequest): DeliveryRoute | null {
  const driver = db.drivers.find((d) => d.id === req.driverId);
  if (!driver) return null;
  const stops = req.orderIds
    .map((oid, i) => {
      const o = db.orders.find((x) => x.id === oid);
      if (!o) return null;
      return {
        orderId: o.id,
        orderCode: o.code,
        customerName: o.customerName,
        address: o.deliveryAddress,
        zoneName: o.deliveryZoneName ?? "",
        sequence: i + 1,
      };
    })
    .filter((s): s is NonNullable<typeof s> => s !== null);

  const route: DeliveryRoute = {
    id: crypto.randomUUID(),
    date: req.date,
    driverId: driver.id,
    driverName: driver.name,
    status: "Planned",
    stops,
  };
  // Replace any existing route for the same driver+date.
  db.routes = db.routes.filter(
    (r) => !(r.driverId === driver.id && r.date === req.date),
  );
  db.routes.push(route);
  return route;
}

export function optimizeRoute(routeId: string): DeliveryRoute | null {
  const route = db.routes.find((r) => r.id === routeId);
  if (!route) return null;
  // Mock optimization: group by zone, then keep insertion order within a zone.
  // The real backend calls Google Routes API for a true VRP sequence.
  const zoneOrder = ["Centar", "Borik", "Lauš", "Starčevica"];
  route.stops = [...route.stops]
    .sort((a, b) => {
      const za = zoneOrder.indexOf(a.zoneName);
      const zb = zoneOrder.indexOf(b.zoneName);
      if (za !== zb) return za - zb;
      return a.sequence - b.sequence;
    })
    .map((s, i) => ({ ...s, sequence: i + 1 }));
  route.status = "Optimized";
  return route;
}

export function routesForDate(date: string): DeliveryRoute[] {
  return db.routes.filter((r) => r.date === date);
}
