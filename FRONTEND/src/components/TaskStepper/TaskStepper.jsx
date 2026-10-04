import React from 'react';
import { useNavigate } from 'react-router-dom';
import { FileCheck2, Mic, UploadCloud, Check } from 'lucide-react';
import { SPEAKER_ACCENT as ACCENT } from '../../constants/theme';

const STEPS = [
  { number: 1, label: 'Duyệt văn bản', path: '/speaker/review-text', icon: FileCheck2 },
  { number: 2, label: 'Ghi âm', path: '/speaker/record-speech', icon: Mic },
  { number: 3, label: 'Gửi bản ghi', path: '/speaker/submit-task', icon: UploadCloud },
];

export default function TaskStepper({ currentStep = 2 }) {
  const navigate = useNavigate();

  return (
    <div className="flex items-start mb-4 [@media(min-height:900px)]:mb-8 font-sans select-none">
      {STEPS.map((step, idx) => {
        const isCompleted = step.number < currentStep;
        const isCurrent = step.number === currentStep;
        const isLocked = step.number > currentStep;
        const Icon = step.icon;

        return (
          <React.Fragment key={step.number}>
            <button
              onClick={() => !isLocked && navigate(step.path)}
              disabled={isLocked}
              className={`flex flex-col items-center gap-1.5 [@media(min-height:900px)]:gap-2.5 shrink-0 ${
                isLocked ? 'cursor-not-allowed' : 'cursor-pointer group'
              }`}
            >
              <span className="relative w-11 h-11 [@media(min-height:900px)]:w-12 [@media(min-height:900px)]:h-12 flex items-center justify-center">
                {isCurrent && (
                  <span className="absolute inset-0 rounded-full animate-pulse" style={{ background: `${ACCENT}33` }} />
                )}
                <span
                  className="relative w-9 h-9 [@media(min-height:900px)]:w-10 [@media(min-height:900px)]:h-10 rounded-full flex items-center justify-center transition-all"
                  style={{
                    // Đã xong: xanh accent đặc + ✓ trắng; bước đang làm phân biệt bằng quầng sáng + icon riêng
                    background: isCurrent || isCompleted ? ACCENT : '#FFFFFF',
                    border: isLocked ? '1.5px solid #E5E2D8' : 'none',
                    boxShadow: isCurrent ? `0 6px 16px ${ACCENT}59` : 'none',
                  }}
                >
                  {isCompleted ? (
                    <Check className="w-[18px] h-[18px] text-white" strokeWidth={2.5} />
                  ) : (
                    <Icon className="w-[18px] h-[18px]" style={{ color: isCurrent ? '#FFFFFF' : '#C2BFB4' }} />
                  )}
                </span>
              </span>
              <span
                className="type-label text-center whitespace-nowrap"
                style={{ color: isCurrent ? '#1F2733' : isCompleted ? '#6E7078' : '#B7B4A9' }}
              >
                {step.label}
              </span>
            </button>

            {idx < STEPS.length - 1 && (
              <span
                className="flex-1 h-[3px] rounded-full mt-[21px] [@media(min-height:900px)]:mt-[23px] mx-2 transition-colors"
                style={{ background: isCompleted ? ACCENT : '#E5E2D8' }}
              />
            )}
          </React.Fragment>
        );
      })}
    </div>
  );
}