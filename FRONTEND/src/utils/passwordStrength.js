/** Điểm mạnh mật khẩu 0-4: đủ 8 ký tự, có chữ, có số, có ký tự đặc biệt hoặc dài từ 12. */
export function getPasswordScore(password = '') {
  if (!password) return 0;
  let score = 0;
  if (password.length >= 8) score += 1;
  if (/[A-Za-z]/.test(password)) score += 1;
  if (/\d/.test(password)) score += 1;
  if (/[^A-Za-z0-9]/.test(password) || password.length >= 12) score += 1;
  return score;
}
