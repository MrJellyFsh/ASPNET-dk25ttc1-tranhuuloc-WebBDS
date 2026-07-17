"use client";

import { useState } from "react";
import { ArrowDown, ArrowUp, ImagePlus, LoaderCircle, Trash2, UploadCloud } from "lucide-react";

type ImageValue = {
  id?: string;
  url: string;
  storageKey?: string | null;
  providerAssetId?: string | null;
};

type ImageDraft = ImageValue & { key: string };

type UploadTicket = {
  provider: "cloudinary";
  endpoint: string;
  apiKey: string;
  timestamp: number;
  signature: string;
  folder: string;
  assetFolder?: string;
  publicIdPrefix?: string;
  context: string;
  transformation: string;
  allowedFormats: string[];
  maxBytes: number;
};

const MAX_IMAGES = 20;
const localMimeTypes = ["image/jpeg", "image/png", "image/webp"];

function initialDrafts(images: ImageValue[]) {
  const drafts = images.map((image, index) => ({ ...image, key: image.id ?? `existing-${index}` }));
  while (drafts.length < 3) drafts.push({ key: `empty-${drafts.length}`, url: "" });
  return drafts;
}

function isUploadTicket(value: unknown): value is UploadTicket {
  if (!value || typeof value !== "object") return false;
  const ticket = value as Record<string, unknown>;
  return ticket.provider === "cloudinary"
    && typeof ticket.endpoint === "string"
    && typeof ticket.apiKey === "string"
    && typeof ticket.timestamp === "number"
    && typeof ticket.signature === "string"
    && typeof ticket.folder === "string"
    && typeof ticket.context === "string"
    && typeof ticket.transformation === "string"
    && Array.isArray(ticket.allowedFormats)
    && typeof ticket.maxBytes === "number";
}

async function responseMessage(response: Response) {
  try {
    const body: unknown = await response.json();
    if (body && typeof body === "object" && typeof (body as { error?: unknown }).error === "string") return (body as { error: string }).error;
  } catch {
    // Keep a generic message when the provider does not return JSON.
  }
  return "Không thể tải ảnh. Vui lòng thử lại.";
}

export function ListingImageUploader({ initialImages = [] }: { initialImages?: ImageValue[] }) {
  const [images, setImages] = useState<ImageDraft[]>(() => initialDrafts(initialImages));
  const [isUploading, setIsUploading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const filledCount = images.filter((image) => image.url.trim()).length;

  function updateImage(key: string, update: Partial<ImageDraft>) {
    setImages((current) => current.map((image) => image.key === key ? { ...image, ...update } : image));
  }

  function removeImage(key: string) {
    setImages((current) => {
      const next = current.filter((image) => image.key !== key);
      while (next.length < 3) next.push({ key: `empty-${Date.now()}-${next.length}`, url: "" });
      return next;
    });
  }

  function moveImage(key: string, direction: -1 | 1) {
    setImages((current) => {
      const index = current.findIndex((image) => image.key === key);
      const target = index + direction;
      if (index < 0 || target < 0 || target >= current.length) return current;
      const next = [...current];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
    });
  }

  function addUploadedImage(image: ImageDraft) {
    setImages((current) => {
      const emptyIndex = current.findIndex((item) => !item.url.trim());
      if (emptyIndex >= 0) {
        const next = [...current];
        next[emptyIndex] = image;
        return next;
      }
      return [...current, image];
    });
  }

  async function uploadFiles(event: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (!files.length) return;
    if (filledCount + files.length > MAX_IMAGES) {
      setMessage(`Mỗi tin đăng có tối đa ${MAX_IMAGES} ảnh.`);
      return;
    }

    setIsUploading(true);
    setMessage(null);
    try {
      for (const file of files) {
        if (!localMimeTypes.includes(file.type)) throw new Error("Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.");
        const signatureResponse = await fetch("/api/uploads/images/sign", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ mimeType: file.type, size: file.size }),
        });
        if (!signatureResponse.ok) throw new Error(await responseMessage(signatureResponse));
        const ticketPayload: unknown = await signatureResponse.json();
        if (!isUploadTicket(ticketPayload)) throw new Error("Phản hồi ký tải ảnh không hợp lệ.");
        if (file.size > ticketPayload.maxBytes) throw new Error("Dung lượng ảnh vượt quá giới hạn cho phép.");

        const uploadBody = new FormData();
        uploadBody.set("file", file);
        uploadBody.set("api_key", ticketPayload.apiKey);
        uploadBody.set("timestamp", String(ticketPayload.timestamp));
        uploadBody.set("signature", ticketPayload.signature);
        if (ticketPayload.assetFolder) uploadBody.set("asset_folder", ticketPayload.assetFolder);
        else uploadBody.set("folder", ticketPayload.folder);
        if (ticketPayload.publicIdPrefix) uploadBody.set("public_id_prefix", ticketPayload.publicIdPrefix);
        uploadBody.set("context", ticketPayload.context);
        uploadBody.set("transformation", ticketPayload.transformation);
        uploadBody.set("allowed_formats", ticketPayload.allowedFormats.join(","));
        const uploadResponse = await fetch(ticketPayload.endpoint, { method: "POST", body: uploadBody });
        if (!uploadResponse.ok) throw new Error(await responseMessage(uploadResponse));
        const uploadPayload: unknown = await uploadResponse.json();
        if (!uploadPayload || typeof uploadPayload !== "object") throw new Error("Cloudinary trả về dữ liệu ảnh không hợp lệ.");
        const uploaded = uploadPayload as { secure_url?: unknown; public_id?: unknown; asset_id?: unknown };
        if (typeof uploaded.secure_url !== "string" || typeof uploaded.public_id !== "string" || typeof uploaded.asset_id !== "string") {
          throw new Error("Cloudinary không trả về định danh ảnh cần thiết.");
        }
        addUploadedImage({
          key: `upload-${uploaded.asset_id}`,
          url: uploaded.secure_url,
          storageKey: uploaded.public_id,
          providerAssetId: uploaded.asset_id,
        });
      }
      setMessage("Đã tải ảnh lên. Nhấn lưu bản nháp để ghi nhận thay đổi.");
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không thể tải ảnh. Vui lòng thử lại.");
    } finally {
      setIsUploading(false);
    }
  }

  return (
    <fieldset className="grid gap-3">
      <legend className="text-lg font-bold text-[#123b38]">Ảnh tin đăng</legend>
      <p className="text-sm text-[#668078]">Tải tối đa {MAX_IMAGES} ảnh JPEG, PNG hoặc WebP. Ở production, ảnh phải được tải trực tiếp qua Cloudinary đã ký; URL HTTPS chỉ dành cho môi trường local chưa cấu hình storage.</p>
      <label className="flex cursor-pointer items-center justify-center gap-2 rounded-lg border border-dashed border-[#8dbcae] bg-[#eef6f2] px-4 py-3 text-sm font-bold text-[#176b5c] hover:bg-[#e4f2ec]">
        {isUploading ? <LoaderCircle size={18} className="animate-spin" /> : <UploadCloud size={18} />}
        {isUploading ? "Đang tải ảnh…" : "Tải ảnh từ thiết bị"}
        <input type="file" accept="image/jpeg,image/png,image/webp" multiple disabled={isUploading || filledCount >= MAX_IMAGES} onChange={uploadFiles} className="sr-only" />
      </label>
      {message ? <p role="status" className="rounded-lg border border-[#cdded6] bg-white px-3 py-2 text-sm text-[#42605a]">{message}</p> : null}
      <div className="grid gap-3">
        {images.map((image, index) => <div key={image.key} className="rounded-lg border border-[#dbe7e1] bg-white p-3">
          <input type="hidden" name="imageUrls" value={image.url} />
          <input type="hidden" name="imageIds" value={image.id ?? ""} />
          <input type="hidden" name="imageStorageKeys" value={image.storageKey ?? ""} />
          <input type="hidden" name="imageProviderAssetIds" value={image.providerAssetId ?? ""} />
          <div className="flex flex-wrap items-center gap-2">
            <label className="min-w-60 flex-1 text-sm font-semibold text-[#254b45]">Đường dẫn ảnh {index + 1}{index < 3 ? " *" : ""}
              <input value={image.url} onChange={(event) => updateImage(image.key, { id: undefined, url: event.target.value, storageKey: undefined, providerAssetId: undefined })} type="url" placeholder="https://..." className="mt-1 w-full rounded-lg border border-[#cdded6] px-3 py-2.5 font-normal outline-none focus:border-[#176b5c]" />
            </label>
            <div className="flex gap-1 self-end pb-0.5">
              <button type="button" onClick={() => moveImage(image.key, -1)} disabled={index === 0} aria-label={`Đưa ảnh ${index + 1} lên`} className="grid h-10 w-10 place-items-center rounded-lg border border-[#cdded6] text-[#176b5c] disabled:cursor-not-allowed disabled:opacity-40"><ArrowUp size={16} /></button>
              <button type="button" onClick={() => moveImage(image.key, 1)} disabled={index === images.length - 1} aria-label={`Đưa ảnh ${index + 1} xuống`} className="grid h-10 w-10 place-items-center rounded-lg border border-[#cdded6] text-[#176b5c] disabled:cursor-not-allowed disabled:opacity-40"><ArrowDown size={16} /></button>
              <button type="button" onClick={() => removeImage(image.key)} aria-label={`Xóa ảnh ${index + 1}`} className="grid h-10 w-10 place-items-center rounded-lg border border-red-200 text-red-700"><Trash2 size={16} /></button>
            </div>
          </div>
        </div>)}
      </div>
      {images.length < MAX_IMAGES ? <button type="button" onClick={() => setImages((current) => [...current, { key: `manual-${Date.now()}`, url: "" }])} className="flex w-fit items-center gap-2 rounded-lg border border-[#b7d0c7] px-3 py-2 text-sm font-bold text-[#176b5c]"><ImagePlus size={17} /> Thêm đường dẫn ảnh</button> : null}
    </fieldset>
  );
}
