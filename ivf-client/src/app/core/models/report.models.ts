export interface DashboardStats {
    totalPatients: number;
    activeCycles: number;
    todayQueueCount: number;
    monthlyRevenue: number;
}

export interface CycleSuccessRates {
    year: number;
    totalCycles: number;
    pregnancies: number;
    notPregnant: number;
    cancelled: number;
    frozenAll: number;
    successRate: number;
}

export interface MonthlyRevenue {
    month: number;
    revenue: number;
}
