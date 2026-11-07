import type { ReactNode } from 'react';
import { ScrollView } from 'react-native';

import { ScreenTitle } from './ScreenTitle';

/** A titled, scrollable page for forms. */
export function FormScreen({ title, children }: { title: string; children: ReactNode }) {
  return (
    <ScrollView contentContainerClassName="gap-md p-lg" keyboardShouldPersistTaps="handled">
      <ScreenTitle title={title} />
      {children}
    </ScrollView>
  );
}
