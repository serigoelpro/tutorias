function toggleView() {
    const cardView = document.getElementById('cardView');
    const tableView = document.getElementById('tableView');
    const btn = document.getElementById('vistaBtn');

    cardView.classList.toggle('hidden');
    tableView.classList.toggle('hidden');

    if (cardView.classList.contains('hidden')) {
        btn.textContent = 'Vista Cuadrada';
    } else {
        btn.textContent = 'Vista Lista';
    }
}

document.getElementById('searchInput').addEventListener('input', function () {
    const searchTerm = this.value.toLowerCase();
    const cards = document.querySelectorAll('#cardView .student-card');
    const rows = document.querySelectorAll('#tableView tbody tr');

    cards.forEach(card => {
        const name = card.querySelector('.student-name').textContent.toLowerCase();
        const matricula = card.querySelector('.student-matricula').textContent.toLowerCase();
        card.style.display = name.includes(searchTerm) || matricula.includes(searchTerm) ? 'block' : 'none';
    });

    rows.forEach(row => {
        const name = row.children[1].textContent.toLowerCase();
        const matricula = row.children[2].textContent.toLowerCase();
        row.style.display = name.includes(searchTerm) || matricula.includes(searchTerm) ? '' : 'none';
    });
});
function toggleView() {
    const cardView = document.getElementById('cardView');
    const tableView = document.getElementById('tableView');
    const btn = document.getElementById('vistaBtn');

    cardView.classList.toggle('hidden');
    tableView.classList.toggle('hidden');

    btn.textContent = cardView.classList.contains('hidden') ? 'Vista Alumno' : 'Vista Tutor';
}

document.getElementById('searchInput').addEventListener('input', function () {
    const searchTerm = this.value.toLowerCase();
    const cards = document.querySelectorAll('#cardView .student-card');
    const rows = document.querySelectorAll('#tableView tbody tr');

    cards.forEach(card => {
        const name = card.querySelector('.student-name').textContent.toLowerCase();
        const matricula = card.querySelector('.student-matricula').textContent.toLowerCase();
        card.style.display = name.includes(searchTerm) || matricula.includes(searchTerm) ? 'block' : 'none';
    });

    rows.forEach(row => {
        const name = row.children[1].textContent.toLowerCase();
        const matricula = row.children[2].textContent.toLowerCase();
        row.style.display = name.includes(searchTerm) || matricula.includes(searchTerm) ? '' : 'none';
    });
});