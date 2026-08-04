export interface ProductRow {
  id: string;
  sku: string;
  name: string;
  description: string | null;
  price: number;
  category_id: string | null;
  is_active: number;
  created_at: string;
  updated_at: string;
}

export interface CategoryRow {
  id: string;
  name: string;
  normalized_name: string;
  description: string | null;
  created_at: string;
  updated_at: string;
}
