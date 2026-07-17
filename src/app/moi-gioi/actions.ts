"use server";

import { ListingStatus, Prisma, Role } from "@prisma/client";
import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { getServerSession } from "next-auth";
import { z } from "zod";
import { authOptions } from "@/lib/auth";
import { logError } from "@/lib/observability/logger";
import { prisma } from "@/lib/prisma";
import { reserveListingPlan } from "@/lib/payments/transactions";
import { imageStorage, type ImageAssetInput, type StoredImage, type StoredImageReference } from "@/lib/storage/image-storage";
import { listingInputSchema } from "@/lib/validators/listing";

const listingIdSchema = z.string().cuid();
const listingSubmissionSchema = z.object({
  id: listingIdSchema,
  planId: z.string().cuid(),
  idempotencyKey: z.uuid(),
});

type ListingImageInput = ImageAssetInput & { id?: string };

type ExistingListingImage = StoredImageReference & {
  id: string;
  url: string;
  alt: string | null;
  width: number | null;
  height: number | null;
  bytes: number | null;
  mimeType: string | null;
};

function text(formData: FormData, name: string) {
  return String(formData.get(name) ?? "").trim();
}

function optionalNumber(formData: FormData, name: string) {
  const value = text(formData, name);
  return value ? Number(value) : undefined;
}

function priceCents(formData: FormData) {
  const priceVnd = Number(text(formData, "priceVnd"));
  if (!Number.isSafeInteger(priceVnd) || priceVnd <= 0 || priceVnd > Number.MAX_SAFE_INTEGER / 100) return Number.NaN;
  return priceVnd * 100;
}

function parseListing(formData: FormData) {
  const imageUrls = formData.getAll("imageUrls").map((value) => String(value).trim());
  const imageIds = formData.getAll("imageIds").map((value) => String(value).trim());
  const imageStorageKeys = formData.getAll("imageStorageKeys").map((value) => String(value).trim());
  const imageProviderAssetIds = formData.getAll("imageProviderAssetIds").map((value) => String(value).trim());
  const imageInputs: ListingImageInput[] = imageUrls.map((url, index) => ({
    id: imageIds[index] || undefined,
    url,
    storageKey: imageStorageKeys[index] || undefined,
    providerAssetId: imageProviderAssetIds[index] || undefined,
  })).filter((image) => Boolean(image.url));

  const parsed = listingInputSchema.safeParse({
    title: text(formData, "title"),
    intent: text(formData, "intent"),
    propertyTypeId: text(formData, "propertyTypeId"),
    priceCents: priceCents(formData),
    areaM2: Number(text(formData, "areaM2")),
    bedrooms: optionalNumber(formData, "bedrooms"),
    bathrooms: optionalNumber(formData, "bathrooms"),
    address: text(formData, "address"),
    district: text(formData, "district"),
    province: text(formData, "province"),
    latitude: optionalNumber(formData, "latitude"),
    longitude: optionalNumber(formData, "longitude"),
    description: text(formData, "description"),
    contactName: text(formData, "contactName"),
    contactPhone: text(formData, "contactPhone"),
    images: imageInputs,
  });
  if (!parsed.success) return parsed;
  return {
    success: true as const,
    data: {
      ...parsed.data,
      images: parsed.data.images.map((image, index) => ({ ...image, id: imageInputs[index]?.id })),
    },
  };
}

async function prepareListingImages(ownerId: string, images: ImageAssetInput[]) {
  try {
    return await imageStorage.prepareImages({ ownerId, images });
  } catch (error) {
    logError("storage.verification_failed", { error });
    return null;
  }
}

function storedImageFromExisting(image: ExistingListingImage): StoredImage {
  return {
    url: image.url,
    alt: image.alt ?? undefined,
    provider: image.provider,
    storageKey: image.storageKey ?? undefined,
    providerAssetId: image.providerAssetId ?? undefined,
    width: image.width ?? undefined,
    height: image.height ?? undefined,
    bytes: image.bytes ?? undefined,
    mimeType: image.mimeType ?? undefined,
  };
}

async function prepareUpdatedListingImages(
  ownerId: string,
  imageInputs: ListingImageInput[],
  existingImages: ExistingListingImage[],
) {
  const existingById = new Map(existingImages.map((image) => [image.id, image]));
  const retainedById = new Map<string, StoredImage>();
  const newImages: ImageAssetInput[] = [];
  const seenIds = new Set<string>();

  for (const image of imageInputs) {
    if (!image.id) {
      newImages.push(image);
      continue;
    }
    if (!listingIdSchema.safeParse(image.id).success || seenIds.has(image.id)) return null;
    seenIds.add(image.id);
    const existingImage = existingById.get(image.id);
    // Never trust form metadata for an existing image. Its ID must belong to
    // this listing and the URL must be unchanged; metadata comes from Prisma.
    if (!existingImage || existingImage.url !== image.url) return null;
    retainedById.set(image.id, storedImageFromExisting(existingImage));
  }

  const verifiedNewImages = await prepareListingImages(ownerId, newImages);
  if (!verifiedNewImages) return null;
  let nextNewImage = 0;
  return imageInputs.map((image) => image.id
    ? retainedById.get(image.id)!
    : verifiedNewImages[nextNewImage++]);
}

async function cleanupRemovedImages(ownerId: string, images: StoredImageReference[]) {
  const candidates = images.filter((image) => image.provider === "CLOUDINARY" && image.providerAssetId && image.storageKey);
  if (!candidates.length) return;
  try {
    // A provider asset is a single-owner resource in this schema. Querying
    // references again after the transaction avoids destroying an asset that
    // another committed listing mutation has retained.
    const references = await prisma.listingImage.findMany({
      where: { provider: "CLOUDINARY", providerAssetId: { in: candidates.map((image) => image.providerAssetId!) } },
      select: { providerAssetId: true },
    });
    const referencedAssetIds = new Set(references.flatMap((image) => image.providerAssetId ? [image.providerAssetId] : []));
    const orphaned = candidates.filter((image) => !referencedAssetIds.has(image.providerAssetId!));
    if (orphaned.length) await imageStorage.deleteImages({ ownerId, images: orphaned });
  } catch (error) {
    // The database mutation has already committed. The logger intentionally
    // excludes asset identifiers, URLs, user data, messages, and stacks.
    logError("storage.cleanup_failed", { error });
  }
}

async function requireAgent() {
  const session = await getServerSession(authOptions);
  if (!session?.user?.id) redirect("/dang-nhap");
  const user = await prisma.user.findFirst({
    where: { id: session.user.id, role: { in: [Role.AGENT, Role.ADMIN] } },
    select: { id: true },
  });
  if (!user) redirect("/tro-thanh-moi-gioi");
  return user;
}

function slugify(value: string) {
  return value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").replace(/Đ/g, "d")
    .toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "").slice(0, 80) || "tin-dang";
}

async function uniqueSlug(title: string, omitId?: string) {
  const base = slugify(title);
  let candidate = base;
  let sequence = 2;
  while (await prisma.listing.findFirst({ where: { slug: candidate, ...(omitId ? { NOT: { id: omitId } } : {}) }, select: { id: true } })) {
    candidate = `${base}-${sequence++}`;
  }
  return candidate;
}

async function hasDuplicate(ownerId: string, title: string, address: string, omitId?: string) {
  return prisma.listing.findFirst({
    where: { ownerId, title: { equals: title, mode: "insensitive" }, address: { equals: address, mode: "insensitive" }, ...(omitId ? { NOT: { id: omitId } } : {}) },
    select: { id: true },
  });
}

function revalidatePublicListing(slug: string) {
  revalidatePath("/");
  revalidatePath("/tim-kiem");
  revalidatePath(`/tin/${slug}`);
}

function isProviderAssetAlreadyUsed(error: unknown) {
  if (!(error instanceof Prisma.PrismaClientKnownRequestError) || error.code !== "P2002") return false;
  const target = error.meta?.target;
  return (Array.isArray(target) && target.includes("provider") && target.includes("providerAssetId"))
    || (typeof target === "string" && target.includes("providerAssetId"));
}

export async function createListing(formData: FormData) {
  const user = await requireAgent();
  const parsed = parseListing(formData);
  if (!parsed.success) redirect("/moi-gioi/tin-moi?error=invalid-input");
  if (await hasDuplicate(user.id, parsed.data.title, parsed.data.address)) redirect("/moi-gioi/tin-moi?error=duplicate");
  const { images: imageInputs, ...listingData } = parsed.data;
  const images = await prepareListingImages(user.id, imageInputs);
  if (!images) redirect("/moi-gioi/tin-moi?error=image-upload");
  try {
    await prisma.listing.create({
      data: { ...listingData, slug: await uniqueSlug(listingData.title), status: ListingStatus.DRAFT, ownerId: user.id, images: { create: images.map((image, sortOrder) => ({ ...image, sortOrder })) } },
    });
  } catch (error) {
    if (isProviderAssetAlreadyUsed(error)) redirect("/moi-gioi/tin-moi?error=image-upload");
    throw error;
  }
  revalidatePath("/moi-gioi");
  redirect("/moi-gioi?created=1");
}

export async function updateListing(id: string, formData: FormData) {
  const user = await requireAgent();
  if (!listingIdSchema.safeParse(id).success) redirect("/moi-gioi?error=not-found");
  const existing = await prisma.listing.findFirst({
    where: { id, ownerId: user.id },
    select: {
      id: true,
      slug: true,
      status: true,
      revision: true,
      images: {
        select: {
          id: true,
          url: true,
          alt: true,
          provider: true,
          storageKey: true,
          providerAssetId: true,
          width: true,
          height: true,
          bytes: true,
          mimeType: true,
        },
      },
    },
  });
  if (!existing) redirect("/moi-gioi?error=not-found");
  if (existing.status === ListingStatus.PENDING_APPROVAL) redirect("/moi-gioi?error=pending-review");
  const parsed = parseListing(formData);
  if (!parsed.success) redirect(`/moi-gioi/tin/${id}?error=invalid-input`);
  if (await hasDuplicate(user.id, parsed.data.title, parsed.data.address, id)) redirect(`/moi-gioi/tin/${id}?error=duplicate`);
  const { images: imageInputs, ...listingData } = parsed.data;
  const images = await prepareUpdatedListingImages(user.id, imageInputs, existing.images);
  if (!images) redirect(`/moi-gioi/tin/${id}?error=image-upload`);
  const slug = await uniqueSlug(listingData.title, id);
  const returnToDraft = existing.status !== ListingStatus.DRAFT;
  let updated: boolean;
  try {
    updated = await prisma.$transaction(async (tx) => {
      const transition = await tx.listing.updateMany({
        // `revision` makes a stale concurrent edit fail instead of allowing
        // its later cleanup to race with a newer image selection.
        where: { id, ownerId: user.id, status: existing.status, revision: existing.revision },
        data: {
          ...listingData,
          slug,
          revision: { increment: 1 },
          ...(returnToDraft ? {
            status: ListingStatus.DRAFT,
            ...(existing.status === ListingStatus.PUBLISHED || existing.status === ListingStatus.EXPIRED || existing.status === ListingStatus.ARCHIVED ? { publishedAt: null, expiresAt: null } : {}),
          } : {}),
        },
      });
      if (!transition.count) return false;
      await tx.listingImage.deleteMany({ where: { listingId: id } });
      await tx.listingImage.createMany({ data: images.map((image, sortOrder) => ({ ...image, sortOrder, listingId: id })) });
      return true;
    });
  } catch (error) {
    if (isProviderAssetAlreadyUsed(error)) redirect(`/moi-gioi/tin/${id}?error=image-upload`);
    throw error;
  }
  if (!updated) redirect("/moi-gioi?error=pending-review");
  const retainedCloudinaryAssetIds = new Set(images
    .filter((image) => image.provider === "CLOUDINARY" && Boolean(image.providerAssetId))
    .map((image) => image.providerAssetId!));
  await cleanupRemovedImages(user.id, existing.images.filter((image) => image.provider === "CLOUDINARY" && !retainedCloudinaryAssetIds.has(image.providerAssetId ?? "")));
  revalidatePath("/moi-gioi");
  revalidatePath(`/moi-gioi/tin/${id}`);
  if (existing.status === ListingStatus.PUBLISHED) {
    revalidatePublicListing(existing.slug);
    revalidatePublicListing(slug);
  }
  redirect("/moi-gioi?updated=1");
}

export async function submitListingForApproval(formData: FormData) {
  const user = await requireAgent();
  const parsed = listingSubmissionSchema.safeParse({
    id: text(formData, "id"),
    planId: text(formData, "planId"),
    idempotencyKey: text(formData, "idempotencyKey"),
  });
  if (!parsed.success) redirect("/moi-gioi?error=invalid-submission");

  // Service thanh toán giữ gói, kiểm quota/trừ ví và đưa tin vào hàng đợi trong cùng transaction.
  const outcome = await reserveListingPlan({
    listingId: parsed.data.id,
    planId: parsed.data.planId,
    idempotencyKey: parsed.data.idempotencyKey,
    userId: user.id,
  });
  if (outcome.code === "listing-unavailable") redirect("/moi-gioi?error=cannot-submit");
  if (outcome.code === "plan-unavailable") redirect("/moi-gioi?error=plan-unavailable");
  if (outcome.code === "quota-exhausted") redirect("/moi-gioi?error=free-quota-exhausted");
  if (outcome.code === "insufficient-wallet") redirect("/moi-gioi?error=insufficient-wallet");
  if (outcome.code === "plan-reserved") redirect("/moi-gioi?error=plan-reserved");
  if (outcome.code === "idempotency-conflict") redirect("/moi-gioi?error=invalid-submission");

  revalidatePath("/moi-gioi");
  revalidatePath("/moi-gioi/vi");
  revalidatePath("/admin");
  redirect("/moi-gioi?submitted=1");
}

export async function deleteListing(formData: FormData) {
  const user = await requireAgent();
  const parsedId = listingIdSchema.safeParse(text(formData, "id"));
  if (!parsedId.success) redirect("/moi-gioi?error=not-found");
  const outcome = await prisma.$transaction(async (tx) => {
    const listing = await tx.listing.findFirst({
      where: { id: parsedId.data, ownerId: user.id },
      select: {
        id: true,
        slug: true,
        status: true,
        images: { select: { provider: true, storageKey: true, providerAssetId: true } },
      },
    });
    if (!listing) return null;

    const archived = await tx.listing.updateMany({
      where: { id: listing.id, ownerId: user.id, status: listing.status },
      data: { status: ListingStatus.ARCHIVED, publishedAt: null, expiresAt: null },
    });
    if (!archived.count) return null;

    const auditCount = await tx.listingAuditLog.count({ where: { listingId: listing.id } });
    if (auditCount) return { slug: listing.slug, archived: true, images: [] as StoredImageReference[] };

    await tx.listing.delete({ where: { id: listing.id } });
    return { slug: listing.slug, archived: false, images: listing.images };
  });
  if (!outcome) redirect("/moi-gioi?error=not-found");
  if (!outcome.archived) await cleanupRemovedImages(user.id, outcome.images);
  revalidatePath("/moi-gioi");
  revalidatePublicListing(outcome.slug);
  redirect(outcome.archived ? "/moi-gioi?archived=1" : "/moi-gioi?deleted=1");
}
