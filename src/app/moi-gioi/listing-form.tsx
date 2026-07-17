import type { PropertyType } from "@prisma/client";
import { ListingImageUploader } from "./listing-image-uploader";

type ListingValues = {
  title?: string; intent?: "SELL" | "RENT"; propertyTypeId?: string; priceCents?: bigint; areaM2?: number;
  bedrooms?: number | null; bathrooms?: number | null; address?: string; district?: string; province?: string;
  latitude?: number | null; longitude?: number | null; description?: string; contactName?: string; contactPhone?: string;
  images?: { id?: string; url: string; storageKey?: string | null; providerAssetId?: string | null }[];
};

export function ListingForm({ propertyTypes, listing, action, submitLabel }: { propertyTypes: PropertyType[]; listing?: ListingValues; action: (formData: FormData) => void | Promise<void>; submitLabel: string }) {
  return <form action={action} className="space-y-6">
    <fieldset className="grid gap-4 sm:grid-cols-2"><legend className="mb-3 text-lg font-bold text-[#123b38]">Thông tin cơ bản</legend>
      <Label name="title" label="Tiêu đề" defaultValue={listing?.title} required className="sm:col-span-2" minLength={20} />
      <Select name="intent" label="Nhu cầu" defaultValue={listing?.intent ?? "SELL"}><option value="SELL">Bán</option><option value="RENT">Cho thuê</option></Select>
      <Select name="propertyTypeId" label="Loại bất động sản" defaultValue={listing?.propertyTypeId} required><option value="">Chọn loại</option>{propertyTypes.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</Select>
      <Label name="priceVnd" label="Giá (đồng)" type="number" defaultValue={listing?.priceCents ? (listing.priceCents / 100n).toString() : undefined} required min="1" step="1" />
      <Label name="areaM2" label="Diện tích (m²)" type="number" defaultValue={listing?.areaM2} required min="0.1" step="0.1" />
      <Label name="bedrooms" label="Phòng ngủ" type="number" defaultValue={listing?.bedrooms ?? undefined} min="0" />
      <Label name="bathrooms" label="Phòng tắm" type="number" defaultValue={listing?.bathrooms ?? undefined} min="0" />
    </fieldset>
    <fieldset className="grid gap-4 sm:grid-cols-2"><legend className="mb-3 text-lg font-bold text-[#123b38]">Vị trí & liên hệ</legend>
      <Label name="address" label="Địa chỉ" defaultValue={listing?.address} required className="sm:col-span-2" />
      <Label name="district" label="Quận/huyện" defaultValue={listing?.district} required />
      <Label name="province" label="Tỉnh/thành phố" defaultValue={listing?.province} required />
      <Label name="latitude" label="Vĩ độ gần đúng (không bắt buộc)" type="number" defaultValue={listing?.latitude ?? undefined} step="0.0001" min="-90" max="90" />
      <Label name="longitude" label="Kinh độ gần đúng (không bắt buộc)" type="number" defaultValue={listing?.longitude ?? undefined} step="0.0001" min="-180" max="180" />
      <Label name="contactName" label="Tên người liên hệ" defaultValue={listing?.contactName} required />
      <Label name="contactPhone" label="Số điện thoại liên hệ" defaultValue={listing?.contactPhone} required type="tel" />
    </fieldset>
    <p className="-mt-2 text-sm text-[#668078]">Nếu thêm tọa độ, hãy dùng vị trí gần đúng. NhaTot chỉ làm tròn và hiển thị ở mức khu vực, không hiển thị địa chỉ hay tọa độ chính xác công khai.</p>
    <label className="block text-sm font-semibold text-[#254b45]">Mô tả<textarea name="description" required minLength={80} maxLength={10000} defaultValue={listing?.description} className="mt-1 min-h-40 w-full rounded-lg border border-[#cdded6] px-3 py-2.5 outline-none focus:border-[#176b5c]" /><span className="mt-1 block font-normal text-[#668078]">Không đưa số điện thoại vào phần mô tả.</span></label>
    <ListingImageUploader initialImages={listing?.images} />
    <button className="rounded-lg bg-[#176b5c] px-5 py-3 font-bold text-white shadow-sm">{submitLabel}</button>
  </form>;
}

function Label({ label, className = "", ...props }: React.InputHTMLAttributes<HTMLInputElement> & { label: string }) { return <label className={`block text-sm font-semibold text-[#254b45] ${className}`}>{label}<input {...props} className="mt-1 w-full rounded-lg border border-[#cdded6] px-3 py-2.5 font-normal outline-none focus:border-[#176b5c]" /></label>; }
function Select({ label, children, ...props }: React.SelectHTMLAttributes<HTMLSelectElement> & { label: string }) { return <label className="block text-sm font-semibold text-[#254b45]">{label}<select {...props} className="mt-1 w-full rounded-lg border border-[#cdded6] bg-white px-3 py-2.5 font-normal outline-none focus:border-[#176b5c]">{children}</select></label>; }
