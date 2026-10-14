// Industrial Unit Converter Pro Controller
const CATEGORIES = {
  pressure: {
    title: 'Áp suất khí nén & Thủy lực (Pressure)',
    baseUnit: 'Pa',
    desc: '1 bar = 100,000 Pa = 100 kPa = 0.1 MPa ≈ 14.5038 psi ≈ 1.0197 kgf/cm² ≈ 750.06 mmHg',
    units: [
      { id: 'bar', name: 'Bar (Tiêu chuẩn công nghiệp)', symbol: 'bar', toBase: (v) => v * 100000, fromBase: (b) => b / 100000, def: 6 },
      { id: 'psi', name: 'Pound / inch vuông (Pound sq in)', symbol: 'psi', toBase: (v) => v * 6894.757, fromBase: (b) => b / 6894.757 },
      { id: 'kpa', name: 'Kilopascal', symbol: 'kPa', toBase: (v) => v * 1000, fromBase: (b) => b / 1000 },
      { id: 'mpa', name: 'Megapascal (Chuẩn thủy lực)', symbol: 'MPa', toBase: (v) => v * 1000000, fromBase: (b) => b / 1000000 },
      { id: 'kgf', name: 'Kilogram lực / cm²', symbol: 'kgf/cm²', toBase: (v) => v * 98066.5, fromBase: (b) => b / 98066.5 },
      { id: 'mmh2o', name: 'Milimet cột nước', symbol: 'mmH2O', toBase: (v) => v * 9.80665, fromBase: (b) => b / 9.80665 },
      { id: 'mmhg', name: 'Milimet thủy ngân (Torr)', symbol: 'mmHg', toBase: (v) => v * 133.322, fromBase: (b) => b / 133.322 },
      { id: 'atm', name: 'Khí quyển tiêu chuẩn', symbol: 'atm', toBase: (v) => v * 101325, fromBase: (b) => b / 101325 },
    ],
  },
  flow: {
    title: 'Lưu lượng khí & chất lỏng (Flow Rate)',
    baseUnit: 'L_min',
    desc: '1 m³/h = 1,000 L/h ≈ 16.667 L/min ≈ 0.5886 CFM. Tính đường kính ống: D = √(4Q / (π * v)).',
    units: [
      { id: 'l_min', name: 'Lít / phút (Liter per minute)', symbol: 'L/min', toBase: (v) => v, fromBase: (b) => b, def: 250 },
      { id: 'm3_h', name: 'Mét khối / giờ', symbol: 'm³/h', toBase: (v) => v * 16.66667, fromBase: (b) => b / 16.66667 },
      { id: 'cfm', name: 'Cubic Feet / Min (CFM Mỹ)', symbol: 'CFM', toBase: (v) => v * 28.3168, fromBase: (b) => b / 28.3168 },
      { id: 'l_s', name: 'Lít / giây', symbol: 'L/s', toBase: (v) => v * 60, fromBase: (b) => b / 60 },
      { id: 'm3_s', name: 'Mét khối / giây', symbol: 'm³/s', toBase: (v) => v * 60000, fromBase: (b) => b / 60000 },
      { id: 'nm3_h', name: 'Normal Mét khối / giờ', symbol: 'Nm³/h', toBase: (v) => v * 16.66667, fromBase: (b) => b / 16.66667 },
    ],
  },
  torque: {
    title: 'Lực & Moment xoắn động cơ (Force & Torque)',
    baseUnit: 'Nm',
    desc: 'Moment xoắn T = 9550 * P(kW) / n(RPM). 1 N·m ≈ 0.10197 kgf·m ≈ 0.73756 lbf·ft. 1 kgf ≈ 9.80665 N.',
    units: [
      { id: 'nm', name: 'Newton mét (Chuẩn động cơ)', symbol: 'N·m', toBase: (v) => v, fromBase: (b) => b, def: 45 },
      { id: 'kgfm', name: 'Kilogram lực mét', symbol: 'kgf·m', toBase: (v) => v * 9.80665, fromBase: (b) => b / 9.80665 },
      { id: 'lbfft', name: 'Pound foot (Hệ Anh)', symbol: 'lbf·ft', toBase: (v) => v * 1.355818, fromBase: (b) => b / 1.355818 },
      { id: 'n', name: 'Newton (Lực đẩy/kéo xi lanh)', symbol: 'N', toBase: (v) => v, fromBase: (b) => b },
      { id: 'kn', name: 'Kilonewton', symbol: 'kN', toBase: (v) => v * 1000, fromBase: (b) => b / 1000 },
      { id: 'kgf', name: 'Kilogram lực', symbol: 'kgf', toBase: (v) => v * 9.80665, fromBase: (b) => b / 9.80665 },
      { id: 'lbf', name: 'Pound lực', symbol: 'lbf', toBase: (v) => v * 4.44822, fromBase: (b) => b / 4.44822 },
    ],
  },
  power: {
    title: 'Công suất & Năng lượng (Power & Energy)',
    baseUnit: 'kW',
    desc: '1 kW = 1,000 W ≈ 1.34102 HP (Mã lực cơ khí) ≈ 3,412.14 BTU/h ≈ 1.176 kVA (Cos φ = 0.85).',
    units: [
      { id: 'kw', name: 'Kilowatt (Công suất điện/động cơ)', symbol: 'kW', toBase: (v) => v, fromBase: (b) => b, def: 7.5 },
      { id: 'hp', name: 'Horsepower (Mã lực / Ngựa)', symbol: 'HP', toBase: (v) => v * 0.74569987, fromBase: (b) => b / 0.74569987 },
      { id: 'w', name: 'Watt', symbol: 'W', toBase: (v) => v / 1000, fromBase: (b) => b * 1000 },
      { id: 'btu_h', name: 'BTU / giờ (Làm mát tủ điện)', symbol: 'BTU/h', toBase: (v) => v / 3412.142, fromBase: (b) => b * 3412.142 },
      { id: 'kva', name: 'Kilovolt-Ampe (Cos φ = 0.85)', symbol: 'kVA', toBase: (v) => v * 0.85, fromBase: (b) => b / 0.85 },
      { id: 'kwh', name: 'Kilowatt giờ (Điện năng tiêu thụ)', symbol: 'kWh', toBase: (v) => v, fromBase: (b) => b },
      { id: 'mj', name: 'Megajoule', symbol: 'MJ', toBase: (v) => v / 3.6, fromBase: (b) => b * 3.6 },
    ],
  },
  temp: {
    title: 'Nhiệt độ (Temperature)',
    baseUnit: 'C',
    desc: 'T(°F) = T(°C) * 9/5 + 32. T(K) = T(°C) + 273.15. 100°C = 212°F = 373.15 K.',
    units: [
      { id: 'c', name: 'Độ C (Celsius)', symbol: '°C', toBase: (v) => v, fromBase: (b) => b, def: 65 },
      { id: 'f', name: 'Độ F (Fahrenheit)', symbol: '°F', toBase: (v) => (v - 32) * 5 / 9, fromBase: (b) => b * 9 / 5 + 32 },
      { id: 'k', name: 'Kelvin', symbol: 'K', toBase: (v) => v - 273.15, fromBase: (b) => b + 273.15 },
    ],
  },
  speed: {
    title: 'Tốc độ & Vận tốc góc (Speed & RPM)',
    baseUnit: 'rpm',
    desc: 'Vận tốc thẳng v = π * D * n / 60000 (m/s). Tần số f = p * n / 60 (p = số cặp cực).',
    units: [
      { id: 'rpm', name: 'Vòng / phút (Revolutions per min)', symbol: 'RPM', toBase: (v) => v, fromBase: (b) => b, def: 1450 },
      { id: 'rad_s', name: 'Radian / giây (Vận tốc góc)', symbol: 'rad/s', toBase: (v) => v * 60 / (2 * Math.PI), fromBase: (b) => b * (2 * Math.PI) / 60 },
      { id: 'hz', name: 'Tần số động cơ 4 cực (2 cặp)', symbol: 'Hz', toBase: (v) => v * 30, fromBase: (b) => b / 30 },
      { id: 'm_s', name: 'Mét / giây (Băng tải D=100mm)', symbol: 'm/s', toBase: (v) => v * 60 / (Math.PI * 0.1), fromBase: (b) => b * (Math.PI * 0.1) / 60 },
      { id: 'mm_s', name: 'Milimet / giây (Xi lanh)', symbol: 'mm/s', toBase: (v) => v / 1000, fromBase: (b) => b * 1000 },
      { id: 'km_h', name: 'Kilomet / giờ', symbol: 'km/h', toBase: (v) => v / 3.6, fromBase: (b) => b * 3.6 },
    ],
  },
};

class UnitConverterApp {
  constructor() {
    this.activeCat = 'pressure';
    this.unitInputs = {};
    this.initElements();
    this.bindEvents();
    this.loadCategory('pressure');
  }

  initElements() {
    this.catTitle = document.getElementById('catTitle');
    this.formulaDesc = document.getElementById('formulaDesc');
    this.unitGrid = document.getElementById('unitGrid');
    this.toast = document.getElementById('toast');
  }

  bindEvents() {
    document.querySelectorAll('.cat-tab').forEach((tab) => {
      tab.addEventListener('click', () => {
        document.querySelectorAll('.cat-tab').forEach((t) => t.classList.remove('active'));
        tab.classList.add('active');
        this.loadCategory(tab.dataset.cat);
      });
    });
  }

  loadCategory(catKey) {
    this.activeCat = catKey;
    const cat = CATEGORIES[catKey];
    this.catTitle.textContent = cat.title;
    this.formulaDesc.textContent = cat.desc;

    this.unitGrid.innerHTML = '';
    this.unitInputs = {};

    let defaultBaseValue = 0;
    const firstUnit = cat.units[0];
    defaultBaseValue = firstUnit.toBase(firstUnit.def || 10);

    cat.units.forEach((u) => {
      const card = document.createElement('div');
      card.className = 'unit-card';
      card.innerHTML = `
        <div class="unit-header">
          <span class="unit-name">${u.name}</span>
          <span class="unit-symbol">${u.symbol}</span>
        </div>
        <div class="input-row">
          <input type="number" step="any" class="unit-input" id="inp_${u.id}" data-unit="${u.id}">
          <button class="btn-copy" data-copy="${u.id}" title="Sao chép giá trị">📋</button>
        </div>
      `;

      const input = card.querySelector('.unit-input');
      this.unitInputs[u.id] = input;

      input.addEventListener('input', () => {
        const val = parseFloat(input.value);
        if (!isNaN(val)) {
          const baseVal = u.toBase(val);
          this.updateAllInputs(baseVal, u.id);
        }
      });

      card.querySelector('.btn-copy').addEventListener('click', () => {
        navigator.clipboard.writeText(input.value);
        this.showToast(`Đã sao chép: ${input.value} ${u.symbol}`);
      });

      this.unitGrid.appendChild(card);
    });

    this.updateAllInputs(defaultBaseValue, null);
  }

  updateAllInputs(baseVal, sourceUnitId) {
    const cat = CATEGORIES[this.activeCat];
    cat.units.forEach((u) => {
      if (u.id !== sourceUnitId) {
        const convVal = u.fromBase(baseVal);
        const formatted = this.formatNumber(convVal);
        this.unitInputs[u.id].value = formatted;
      }
    });
  }

  formatNumber(num) {
    if (isNaN(num)) return '';
    if (Math.abs(num) >= 1000000 || (Math.abs(num) < 0.001 && num !== 0)) {
      return num.toExponential(4);
    }
    return parseFloat(num.toFixed(4)).toString();
  }

  showToast(msg) {
    this.toast.textContent = msg;
    this.toast.classList.add('show');
    setTimeout(() => this.toast.classList.remove('show'), 2000);
  }
}

document.addEventListener('DOMContentLoaded', () => new UnitConverterApp());
