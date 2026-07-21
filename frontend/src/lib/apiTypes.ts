/**
 * Bentuk minimal DTO backend yang benar-benar dipakai FE — cermin sebagian
 * Vokasia.Api/Endpoints/Dtos.cs (camelCase, System.Text.Json default policy ASP.NET Core).
 * Sengaja TIDAK menyalin seluruh kontrak backend field-demi-field: tambah field di sini hanya
 * saat ada halaman FE yang benar memakainya, supaya tidak ada tipe basi yang diam-diam menyimpang
 * dari DTO asli C#.
 */

export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface PeriodSummary {
  id: string;
  name: string;
  startDate: string;
  status: "Draft" | "Active" | "Assessment" | "Closed";
}
