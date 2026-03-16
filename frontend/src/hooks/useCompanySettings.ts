import { useQuery } from '@tanstack/react-query';
import apiClient from '../api/apiClient';

export type CompanySettings = {
  id: string;
  name: string;
  taxId: string;
  thankYouMessage?: string;
  taxPercentage?: number;
  currencySymbol?: string;
};

type CompanySettingsResponse = {
  success: boolean;
  data: CompanySettings;
};

export const useCompanySettings = () => {
  return useQuery({
    queryKey: ['company-settings-global'],
    queryFn: async () => {
      const response = await apiClient.get<CompanySettingsResponse>('/core/company/settings');
      return response.data.data;
    },
    staleTime: 60_000,
  });
};
