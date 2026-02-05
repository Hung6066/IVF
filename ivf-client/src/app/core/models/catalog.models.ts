export interface ServiceItem {
    id: string;
    code: string;
    name: string;
    category: string;
    unitPrice: number;
    unit: string;
    description?: string;
    isActive: boolean;
}

export interface ServiceListResponse {
    items: ServiceItem[];
    total: number;
    page: number;
    pageSize: number;
}
