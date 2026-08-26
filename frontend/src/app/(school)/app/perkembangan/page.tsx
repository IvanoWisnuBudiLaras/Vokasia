import { fetcher } from "@/lib/fetcher";
import type { TeacherMonitoringWorkspaceDto } from "@/lib/apiTypes";
import { TeacherLearningRecord } from "./TeacherLearningRecord";

export const dynamic = "force-dynamic";

export default async function TeacherPerkembanganPage() {
  const workspace = await fetcher<TeacherMonitoringWorkspaceDto>("/teacher/learning-record/monitoring");
  return <TeacherLearningRecord initialWorkspace={workspace} />;
}
