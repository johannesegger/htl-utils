export const store = (key: string, value: string | undefined) => {
  if (value === undefined) {
    localStorage.removeItem(key)
  }
  else {
    localStorage.setItem(key, value)
  }
}

const defaults = {
  'student-letter-text': `<h1 class="title">
<span class="test-count-single">Termin für Wiederholungsprüfung</span>
<span class="test-count-multiple">Termine für Wiederholungsprüfungen</span>
</h1>

<p>
<span class="gender-female">Sehr geehrte Frau {{lastName}},</span>
<span class="gender-male">Sehr geehrter Herr {{lastName}},</span>
<span class="gender-unknown">Sehr geehrte Schülerin, sehr geehrter Schüler,</span>
</p>

<p>
hiermit erhalten Sie
<span class="test-count-single">Ihren Wiederholungsprüfungstermin</span>
<span class="test-count-multiple">Ihre Wiederholungsprüfungstermine</span>
<span class="room-available">inkl. Raumeinteilung.</span>.
</p>

<p>Ihre Prüferin bzw. ihr Prüfer kann den mündlichen Termin ihrer Prüfung in Abstimmung mit Ihnen verschieben bzw. vorziehen, falls andere Kandidatinnen bzw. Kandidaten nicht erscheinen.
Bitte sprechen Sie sich dahingehend ab.</p>

<p class="room-unavailable">Für die Raumeinteilung merken Sie sich bitte die Identnummer Ihrer Prüfung. Unter dieser Identnummer ist die Prüfungseinteilung dann kurz vor Schulbeginn anonymisiert öffentlich einsehbar und Sie finden dann auch den Ort Ihrer Prüfung in dieser Liste.</p>

<p class="test-count-single">Viel Erfolg bei Ihrer Prüfung.</p>
<p class="test-count-multiple">Viel Erfolg bei Ihren Prüfungen.</p>`,
  'student-letter-mail-subject': 'Einteilung zu Wiederholungsprüfungen',
  'student-letter-mail-text': `Liebe Schülerinnen und Schüler,

im Anhang findet ihr die Einteilung zu euren Wiederholungsprüfungen inkl. Raumeinteilung.

Viel Erfolg bei den Prüfungen und einen guten Start ins neue Schuljahr.`,
  'teacher-letter-text': `<h1 class="title">Termine für WH-Prüfungen - {{date}}</h1>
<p>Liebe Kolleginnen und Kollegen,</p>
<p>nachfolgend findet ihr die Liste eurer Wiederholungsprüfungen. Kontrolliert sie bitte auf Vollständigkeit und Korrektheit.</p>
<p>Zur Info: In Schularbeitenfächern gibt es einen schriftlichen und einen mündlichen Teil und diese werden auch getrennt voneinander ausgewiesen. In Testfächern ist nur eine mündliche Prüfung erlaubt und so ausgewiesen.</p>
<p class="room-unavailable">Die Saaleinteilung erfolgt im Herbst, sobald Stundenplan und Raumbelegungen festgelegt sind. Ihr bekommt dann dieses Schreiben, um die Information der Prüfungsräumlichkeit ergänzt, ein weiteres Mal.</p>
<p>Bitte gebt diese Liste ausgefüllt bei STAL ab, sobald ihr mit den Prüfungen fertig seid.</p>
<p>Bitte gebt außerdem möglichst zeitnah nach den jeweiligen Prüfungen das zugehörige Prüfungsprotokoll bei STET bzw. im Konferenzzimmer ab.</p>
<p>Viel Erfolg bei den Prüfungen.</p>`,
  'teacher-letter-mail-subject': 'Wiederholungsprüfungen',
  'teacher-letter-mail-text': `Liebe Kolleginnen und Kollegen,

im Anhang findet ihr die finale Einteilung zu euren Wiederholungsprüfungen inkl. Raumzuteilung.

Viel Erfolg bei den Prüfungen und einen guten Start ins neue Schuljahr.`
}

export const loadOrDefault = (key: keyof typeof defaults) => {
  return localStorage.getItem(key) || defaults[key]
}
